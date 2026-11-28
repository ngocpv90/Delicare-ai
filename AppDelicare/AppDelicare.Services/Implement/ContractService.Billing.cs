using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Serilog;

namespace AppDelicare.Services.Implement
{
    public partial class ContractService
    {
        /// <summary>D-78 (2): đổi cách tính tiền đối soát của hợp đồng (mọi trạng thái trừ Hết hạn / Chấm dứt) – áp cho lần đối soát sau.</summary>
        public async Task<IResult<ContractDetailModel>> UpdateBillingBasis(ContractBillingBasisRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var contract = _unitOfWork.ContractRepository.GetSingle(c => c.Id == request.Id && !c.IsDeleted);
                string? error = contract == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : contract.Status is ContractStatus.Expired ? string.Format(ConstantHelper.Message.ContractEditOnlyDraft, ContractStatus.Label(contract.Status))
                    : !ContractConfig.IsValid(request.BillingBasis) ? ConstantHelper.Message.ContractBillingBasisInvalid
                    : null;
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ContractDetailModel>.Fail(error);
                }
                contract!.ConfigJson = ContractConfig.WithBillingBasis(contract.ConfigJson, request.BillingBasis);
                _unitOfWork.ContractRepository.Update(contract);
                await _unitOfWork.ContractRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ContractDetailModel>.Success((await GetDetail(contract.Id)).Data!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateBillingBasis: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>UI-6.51 (D-137 f, GAP-PUR-STD): bật / tắt "Khách chỉ dùng nguyên liệu chuẩn" (mọi trạng thái trừ Hết hạn) – kế hoạch mua không thay thế phần suất của khách này từ lần tính sau.</summary>
        public async Task<IResult<ContractDetailModel>> UpdateStandardIngredientOnly(ContractStandardOnlyRequest request)
        {
            var contract = _unitOfWork.ContractRepository.GetSingle(c => c.Id == request.Id && !c.IsDeleted);
            if (contract == null)
            {
                return Result<ContractDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (contract.Status is ContractStatus.Expired)
            {
                return Result<ContractDetailModel>.Fail(string.Format(ConstantHelper.Message.ContractEditOnlyDraft, ContractStatus.Label(contract.Status)));
            }
            if (contract.StandardIngredientOnly != request.StandardIngredientOnly)
            {
                contract.StandardIngredientOnly = request.StandardIngredientOnly;
                _unitOfWork.ContractRepository.Update(contract);
                await _unitOfWork.ContractRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
            return Result<ContractDetailModel>.Success((await GetDetail(contract.Id)).Data!, request.StandardIngredientOnly ? "Đã bật: khách chỉ dùng nguyên liệu chuẩn" : "Đã tắt: khách được dùng nguyên liệu thay thế theo công thức");
        }
    }
}
