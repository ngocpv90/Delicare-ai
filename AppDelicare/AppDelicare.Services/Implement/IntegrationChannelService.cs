// Viết tay từ BOT-1.1 (D-62): che khóa bí mật trong ConfigJson khi đọc, giữ bí mật cũ khi form gửi lại "***" – trước đây do tools/AppDelicare.CodeGen sinh.
#nullable enable
using AutoMapper;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class IntegrationChannelService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard) : IIntegrationChannelService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<IntegrationChannelViewModel>> GetAll(IntegrationChannelSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ChannelStatus", request.ChannelStatus);
                dynamicParameters.Add("@OwnerUserId", request.OwnerUserId);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<IntegrationChannelViewModel>("uspIntegrationChannel_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                    x.ConfigJson = MessagingRules.MaskJson(x.ConfigJson); // BOT-1.1: không trả token / secret / key
                });
                return new Results<IntegrationChannelViewModel>
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

        public IResults<IntegrationChannel> GetAllData()
        {
            try
            {
                var list = _unitOfWork.IntegrationChannelRepository.GetAll().AsNoTracking().Where(x => !x.IsDeleted).ToList();
                list.ForEach(x => x.ConfigJson = MessagingRules.MaskJson(x.ConfigJson)); // BOT-1.1: bản không theo dõi → không ghi ngược DB
                return new Results<IntegrationChannel>
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

        public async Task<IResult<bool>> Save(IntegrationChannelRequest request, string typeSave)
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
          break;                    default:
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

        private async Task<string> Insert(IntegrationChannelRequest request, string typeSave)
        {
            // MA-1.7 (D-63 k): mã nhóm G2 – chữ hoa không dấu A–Z 0–9 - _ (CodeRules).
            var (normalizedCode, codeError) = CodeRules.NormalizeUserCode(request.ChannelCode);
            if (codeError != null)
            {
                return codeError;
            }
            request.ChannelCode = normalizedCode;
            if (_unitOfWork.IntegrationChannelRepository.GetSingle(x => x.ChannelCode == request.ChannelCode && !x.IsDeleted) != null)
            {
                return "Dữ liệu trùng (ChannelCode) – vi phạm UQ_IntegrationChannel_Code.";
            }
            var insert = _mapper.Map<IntegrationChannel>(request);
            if (request.EnvironmentCode == null) insert.EnvironmentCode = "Production";
            if (request.ChannelStatus == null) insert.ChannelStatus = "Inactive";
            var entity = insert;
            _unitOfWork.IntegrationChannelRepository.Add(insert);
            await _unitOfWork.IntegrationChannelRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(IntegrationChannelRequest request, string typeSave)
        {
            var exist = _unitOfWork.IntegrationChannelRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.7 (D-63 k): mã nhóm G2 – chữ hoa không dấu A–Z 0–9 - _ (CodeRules).
            var (normalizedCode, codeError) = CodeRules.NormalizeUserCode(request.ChannelCode);
            if (codeError != null)
            {
                return codeError;
            }
            request.ChannelCode = normalizedCode;
            if (!string.IsNullOrEmpty(exist.ChannelCode) && !string.Equals(exist.ChannelCode, request.ChannelCode, StringComparison.Ordinal) && (await _referenceGuard.FindUsagesAsync(nameof(IntegrationChannel), exist.Id)).Count > 0)
            {
                return string.Format(ConstantHelper.Message.UserCodeLocked, exist.ChannelCode);
            }
            if (_unitOfWork.IntegrationChannelRepository.GetSingle(x => x.Id != request.Id && x.ChannelCode == request.ChannelCode && !x.IsDeleted) != null)
            {
                return "Dữ liệu trùng (ChannelCode) – vi phạm UQ_IntegrationChannel_Code.";
            }
            string? oldConfig = exist.ConfigJson;
            var entity = _mapper.Map(request, exist);
            // BOT-1.1: form nhận ConfigJson đã che → giá trị "***" giữ bí mật đang lưu.
            entity.ConfigJson = KeepMaskedSecrets(request.ConfigJson, oldConfig);
            _unitOfWork.IntegrationChannelRepository.Update(entity);
            await _unitOfWork.IntegrationChannelRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("IntegrationChannel", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.IntegrationChannelRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.IntegrationChannelRepository.Update(delete);
            await _unitOfWork.IntegrationChannelRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<IntegrationChannel>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.IntegrationChannelRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (data != null)
                {
                    data.ConfigJson = MessagingRules.MaskJson(data.ConfigJson); // BOT-1.1
                }
                if (data == null)
                {
                    return new Result<IntegrationChannel>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<IntegrationChannel>
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

        /// <summary>Khóa bí mật mang giá trị "***" (bản đã che) → lấy lại giá trị đang lưu; JSON lỗi → giữ nguyên chuỗi người dùng nhập (validate ở màn hình).</summary>
        public static string? KeepMaskedSecrets(string? incoming, string? stored)
        {
            if (string.IsNullOrWhiteSpace(incoming) || string.IsNullOrWhiteSpace(stored) || !incoming.Contains(MessagingRules.Masked))
            {
                return incoming;
            }
            try
            {
                if (JsonNode.Parse(incoming) is not JsonObject inObj || JsonNode.Parse(stored) is not JsonObject oldObj)
                {
                    return incoming;
                }
                Restore(inObj, oldObj);
                return inObj.ToJsonString();
            }
            catch (System.Text.Json.JsonException)
            {
                return incoming;
            }
        }

        private static void Restore(JsonObject incoming, JsonObject stored)
        {
            foreach (var key in incoming.Select(p => p.Key).ToList())
            {
                if (incoming[key] is JsonValue v && v.TryGetValue<string>(out var str) && str == MessagingRules.Masked && stored[key] != null)
                {
                    incoming[key] = stored[key]!.DeepClone();
                }
                else if (incoming[key] is JsonObject child && stored[key] is JsonObject oldChild)
                {
                    Restore(child, oldChild);
                }
            }
        }
    }
}
