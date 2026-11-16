using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Implement
{
    public class UomConversionService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IUomConversionService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<UomConversionViewModel>> GetAll(UomConversionSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<UomConversionViewModel>("uspUomConversion_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<UomConversionViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<UomConversion> GetAllData()
        {
            try
            {
                var list = _unitOfWork.UomConversionRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<UomConversion>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(UomConversionRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
                    case ConstantHelper.TypeSave.Insert:
                        result = await Insert(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Update:
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<string> Insert(UomConversionRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<UomConversion>(request);
            _unitOfWork.UomConversionRepository.Add(insert);
            await _unitOfWork.UomConversionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(UomConversionRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.UomConversionRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.UomConversionRepository.Update(update);
            await _unitOfWork.UomConversionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(UomConversion), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.UomConversionRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.UomConversionRepository.Update(delete);
            await _unitOfWork.UomConversionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<UomConversion>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.UomConversionRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<UomConversion>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<UomConversion>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T45R (spec 4.5): hệ số &gt; 0; 2 đơn vị khác nhau; không trùng cặp.</summary>
        private async Task<string> CheckRules(UomConversionRequest request)
        {
            if (request.ConversionFactor <= 0)
            {
                return ConstantHelper.Message.ConversionFactorPositive;
            }
            if (request.FromUnitId == request.ToUnitId)
            {
                return ConstantHelper.Message.ConversionSameUnit;
            }
            if (await _unitOfWork.UomConversionRepository.GetAll()
                .AnyAsync(x => x.FromUnitId == request.FromUnitId && x.ToUnitId == request.ToUnitId && x.Id != request.Id && !x.IsDeleted))
            {
                return ConstantHelper.Message.ConversionExist;
            }
            return string.Empty;
        }

        /// <summary>
        /// T45R (spec 4.5): quy đổi số lượng giữa 2 đơn vị – chiều xuôi (× hệ số), ngược (÷ hệ số), qua đơn vị trung gian (đường ngắn nhất);
        /// chỉ dùng quy đổi đang hiệu lực tại ngày <paramref name="on"/> (mặc định hôm nay). Không có đường quy đổi → null.
        /// </summary>
        public async Task<decimal?> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId, DateTime? on = null)
        {
            if (fromUnitId == toUnitId)
            {
                return quantity;
            }
            DateTime day = (on ?? DateTime.Today).Date;
            var rules = await _unitOfWork.UomConversionRepository.GetAll().AsNoTracking()
                .Where(x => !x.IsDeleted && x.FromUnitId != Guid.Empty && x.ToUnitId != Guid.Empty && x.ConversionFactor > 0
                    && (x.EffectiveFrom == null || x.EffectiveFrom <= day) && (x.EffectiveTo == null || x.EffectiveTo >= day))
                .Select(x => new { From = x.FromUnitId, To = x.ToUnitId, x.ConversionFactor }).ToListAsync();
            var edges = rules.SelectMany(r => new[] { (r.From, r.To, Factor: r.ConversionFactor), (From: r.To, To: r.From, Factor: 1 / r.ConversionFactor) })
                .GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.ToList());
            var best = new Dictionary<Guid, decimal> { [fromUnitId] = 1m };
            var queue = new Queue<Guid>([fromUnitId]);
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                if (u == toUnitId)
                {
                    return quantity * best[u];
                }
                foreach (var e in edges.GetValueOrDefault(u) ?? [])
                {
                    if (!best.ContainsKey(e.To))
                    {
                        best[e.To] = best[u] * e.Factor;
                        queue.Enqueue(e.To);
                    }
                }
            }
            return null;
        }

        /// <summary>T45R: API quy đổi – không có quy đổi → ResponseCode 400 "Chưa có quy đổi…".</summary>
        public async Task<IResult<decimal>> ConvertQuantity(UomConvertRequest request)
        {
            try
            {
                var value = await ConvertAsync(request.Quantity, request.FromUnitId, request.ToUnitId, request.On);
                return value == null
                    ? Result<decimal>.Fail(ConstantHelper.Message.UomConversionMissing)
                    : Result<decimal>.Success(value.Value, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ConvertQuantity: ", ex.ToString()));
                throw;
            }
        }
    }
}
