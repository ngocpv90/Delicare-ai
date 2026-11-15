using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Application.Common
{
    /// <summary>Yêu cầu gửi qua <see cref="IDispatcher"/>. Dùng <see cref="ICommand{TResult}"/> (ghi) hoặc <see cref="IQuery{TResult}"/> (đọc).</summary>
    public interface IRequest<TResult>;

    /// <summary>Lệnh ghi: validate → (máy trạng thái / ngưỡng) → ghi trong transaction → phát sự kiện sau khi thành công.</summary>
    public interface ICommand<TResult> : IRequest<TResult>;

    /// <summary>Truy vấn chỉ đọc.</summary>
    public interface IQuery<TResult> : IRequest<TResult>;

    public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
    {
        Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>Sự kiện nghiệp vụ, phát SAU KHI lệnh thành công (đã commit).</summary>
    public interface IDomainEvent;

    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
    }

    /// <summary>Handler gọi <see cref="Raise"/>; dispatcher phát khi lệnh ngoài cùng chạy xong không lỗi. Lệnh lỗi → sự kiện bị hủy.</summary>
    public interface IDomainEventCollector
    {
        void Raise(IDomainEvent domainEvent);
    }

    /// <summary>Input không hợp lệ (FluentValidation). WebApi đổi thành 400 VALIDATION_FAILED, mỗi lỗi một <c>field</c>.</summary>
    public sealed class ValidationFailedException(IReadOnlyList<(string Field, string Message)> errors)
        : Exception("Dữ liệu không hợp lệ: " + string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}")))
    {
        public IReadOnlyList<(string Field, string Message)> Errors { get; } = errors;
    }

    public interface IDispatcher
    {
        Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Dispatcher trong tiến trình (thay MediatR – từ v13 là license thương mại). Pipeline: tìm handler → chạy mọi validator →
    /// handler → nếu là lệnh ngoài cùng và thành công thì phát các sự kiện đã Raise.
    /// </summary>
    public sealed class Dispatcher(IServiceProvider services) : IDispatcher, IDomainEventCollector
    {
        private readonly List<IDomainEvent> _pending = [];
        private int _depth;

        public void Raise(IDomainEvent domainEvent) => _pending.Add(domainEvent);

        public async Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Type requestType = request.GetType();

            await ValidateAsync(request, requestType, cancellationToken);

            var wrapper = (RequestWrapper<TResult>)Activator.CreateInstance(typeof(RequestWrapper<,>).MakeGenericType(requestType, typeof(TResult)))!;

            _depth++;
            TResult result;
            try
            {
                result = await wrapper.HandleAsync(request, services, cancellationToken);
            }
            catch
            {
                if (_depth == 1)
                {
                    _pending.Clear();
                }
                throw;
            }
            finally
            {
                _depth--;
            }

            if (_depth == 0 && _pending.Count > 0)
            {
                await PublishPendingAsync(cancellationToken);
            }
            return result;
        }

        private async Task ValidateAsync(object request, Type requestType, CancellationToken cancellationToken)
        {
            var validators = (IEnumerable<IValidator>)services.GetServices(typeof(IValidator<>).MakeGenericType(requestType));
            var errors = new List<(string, string)>();
            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(request), cancellationToken);
                errors.AddRange(result.Errors.Select(e => (Camel(e.PropertyName), e.ErrorMessage)));
            }
            if (errors.Count > 0)
            {
                throw new ValidationFailedException(errors);
            }
        }

        private async Task PublishPendingAsync(CancellationToken cancellationToken)
        {
            var events = _pending.ToList();
            _pending.Clear();
            foreach (var domainEvent in events)
            {
                var wrapper = (EventWrapper)Activator.CreateInstance(typeof(EventWrapper<>).MakeGenericType(domainEvent.GetType()))!;
                await wrapper.PublishAsync(domainEvent, services, cancellationToken);
            }
        }

        private static string Camel(string name)
            => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Gọi handler kiểu mạnh (không dùng reflection Invoke → exception đi ra nguyên vẹn).</summary>
    internal abstract class RequestWrapper<TResult>
    {
        public abstract Task<TResult> HandleAsync(IRequest<TResult> request, IServiceProvider services, CancellationToken cancellationToken);
    }

    internal sealed class RequestWrapper<TRequest, TResult> : RequestWrapper<TResult> where TRequest : IRequest<TResult>
    {
        public override Task<TResult> HandleAsync(IRequest<TResult> request, IServiceProvider services, CancellationToken cancellationToken)
            => (services.GetService<IRequestHandler<TRequest, TResult>>()
                ?? throw new InvalidOperationException($"Chưa đăng ký handler cho {typeof(TRequest).Name}."))
                .HandleAsync((TRequest)request, cancellationToken);
    }

    internal abstract class EventWrapper
    {
        public abstract Task PublishAsync(IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken);
    }

    internal sealed class EventWrapper<TEvent> : EventWrapper where TEvent : IDomainEvent
    {
        public override async Task PublishAsync(IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
