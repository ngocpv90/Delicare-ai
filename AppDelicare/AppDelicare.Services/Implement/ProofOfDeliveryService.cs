using AutoMapper;
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

namespace AppDelicare.Services.Implement
{
    public class ProofOfDeliveryService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IProofOfDeliveryService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<ProofOfDeliveryViewModel>> GetAll(ProofOfDeliverySearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DeliveryOrderId", request.DeliveryOrderId);
                dynamicParameters.Add("@ConfirmedAt", request.ConfirmedAt);
                //dynamicParameters.Add("@ReceiverName", request.ReceiverName);
                //dynamicParameters.Add("@ReceiverPhone", request.ReceiverPhone);
                //dynamicParameters.Add("@DeliveredQuantity", request.DeliveredQuantity);
                //dynamicParameters.Add("@AcceptedQuantity", request.AcceptedQuantity);
                //dynamicParameters.Add("@RejectedQuantity", request.RejectedQuantity);
                //dynamicParameters.Add("@SignatureUrl", request.SignatureUrl);
                //dynamicParameters.Add("@PhotoUrl", request.PhotoUrl);
                //dynamicParameters.Add("@Remark", request.Remark);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ProofOfDeliveryViewModel>("uspProofOfDelivery_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ProofOfDeliveryViewModel>
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

        public IResults<ProofOfDelivery> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ProofOfDeliveryRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ProofOfDelivery>
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

        public async Task<IResult<bool>> Save(ProofOfDeliveryRequest request, string typeSave)
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

        /// <summary>T19: biên bản giao nhận (POD) chỉ ghi khi bàn giao (DeliveryStop/Deliver, kiểm nhiệt độ ≥ 60 °C) và không sửa / xóa – là căn cứ đối soát.</summary>
        private static Task<string> Blocked()
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Biên bản giao nhận (POD)", "DeliveryStop/Deliver"));

        private Task<string> Insert(ProofOfDeliveryRequest request, string typeSave) => Blocked();

        private Task<string> Update(ProofOfDeliveryRequest request, string typeSave) => Blocked();

        private Task<string> Delete(Guid? id, string typeSave) => Blocked();

        public async Task<IResult<ProofOfDelivery>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ProofOfDeliveryRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ProofOfDelivery>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ProofOfDelivery>
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
    }
}
