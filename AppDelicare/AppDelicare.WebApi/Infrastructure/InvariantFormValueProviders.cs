using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// FE-2 follow-up: dữ liệu form (<c>[FromForm]</c>) luôn đọc theo <see cref="CultureInfo.InvariantCulture"/> – mặc định ASP.NET Core đọc form
    /// theo văn hóa hiện tại của máy chủ, máy chủ vi-VN sẽ hiểu "12.5" thành 125. Query string vốn đã dùng InvariantCulture.
    /// Chỉ đổi cách đọc dữ liệu vào; định dạng dữ liệu ra (JSON, Excel…) không đổi.
    /// </summary>
    public sealed class InvariantCultureValueProviderFactory(IValueProviderFactory inner) : IValueProviderFactory
    {
        public IValueProviderFactory Inner { get; } = inner;

        public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
        {
            int before = context.ValueProviders.Count;
            await Inner.CreateValueProviderAsync(context);
            for (int i = before; i < context.ValueProviders.Count; i++)
            {
                context.ValueProviders[i] = new InvariantCultureValueProvider(context.ValueProviders[i]);
            }
        }

        /// <summary>Bọc các factory đọc form (FormValueProviderFactory, JQueryFormValueProviderFactory) trong danh sách của MVC.</summary>
        public static void Apply(IList<IValueProviderFactory> factories)
        {
            for (int i = 0; i < factories.Count; i++)
            {
                if (factories[i] is FormValueProviderFactory or JQueryFormValueProviderFactory)
                {
                    factories[i] = new InvariantCultureValueProviderFactory(factories[i]);
                }
            }
        }
    }

    /// <summary>Giữ nguyên giá trị / khóa của provider gốc, chỉ thay văn hóa của kết quả bằng InvariantCulture.</summary>
    public sealed class InvariantCultureValueProvider(IValueProvider inner) : IEnumerableValueProvider, IBindingSourceValueProvider, IKeyRewriterValueProvider
    {
        public bool ContainsPrefix(string prefix) => inner.ContainsPrefix(prefix);

        public ValueProviderResult GetValue(string key)
        {
            var result = inner.GetValue(key);
            return result == ValueProviderResult.None ? result : new ValueProviderResult(result.Values, CultureInfo.InvariantCulture);
        }

        public IDictionary<string, string> GetKeysFromPrefix(string prefix)
            => inner is IEnumerableValueProvider enumerable ? enumerable.GetKeysFromPrefix(prefix) : new Dictionary<string, string>();

        public IValueProvider? Filter(BindingSource bindingSource)
        {
            if (inner is not IBindingSourceValueProvider source)
            {
                return this;
            }
            var filtered = source.Filter(bindingSource);
            return filtered == null ? null : ReferenceEquals(filtered, inner) ? this : new InvariantCultureValueProvider(filtered);
        }

        public IValueProvider? Filter()
        {
            if (inner is not IKeyRewriterValueProvider rewriter)
            {
                return this;
            }
            var filtered = rewriter.Filter();
            return filtered == null ? null : ReferenceEquals(filtered, inner) ? this : new InvariantCultureValueProvider(filtered);
        }
    }
}
