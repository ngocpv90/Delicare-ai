using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-6.33 (D-114): "Tạo phiên bản mới" bộ kiểm tra ATTP – chép bộ + tiêu chí sang phiên bản +1 cùng mã, áp dụng từ ngày chọn (≥ mai); bản cũ hết hiệu lực ngày trước đó.</summary>
    public partial class FoodSafetyChecklistService
    {
        public async Task<IResult<FoodSafetyChecklist>> NewVersion(Guid id, DateTime? effectiveFrom)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var old = _unitOfWork.FoodSafetyChecklistRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (old == null)
                {
                    return Fail(ConstantHelper.Message.ChecklistNotFound);
                }
                DateTime from = (effectiveFrom ?? _clock.Today.AddDays(1)).Date;
                if (from <= _clock.Today)
                {
                    return Fail(ConstantHelper.Message.ChecklistNewVersionDate);
                }
                int version = (await db.FoodSafetyChecklist.AsNoTracking().Where(c => !c.IsDeleted && c.Code == old.Code).MaxAsync(c => (int?)c.VersionNo) ?? old.VersionNo) + 1;
                var copy = new FoodSafetyChecklist
                {
                    Id = Guid.NewGuid(), Code = old.Code, Name = old.Name, VersionNo = version, AreaCode = old.AreaCode, FrequencyCode = old.FrequencyCode,
                    EffectiveFrom = from, EffectiveTo = old.EffectiveTo != null && old.EffectiveTo >= from ? old.EffectiveTo : null, Status = "Active", Note = old.Note,
                };
                _unitOfWork.FoodSafetyChecklistRepository.Add(copy);
                foreach (var i in await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => !i.IsDeleted && i.ChecklistId == old.Id).OrderBy(i => i.SortOrder).ToListAsync())
                {
                    _unitOfWork.FoodSafetyChecklistItemRepository.Add(new FoodSafetyChecklistItem
                    {
                        Id = Guid.NewGuid(), ChecklistId = copy.Id, ItemCode = i.ItemCode, ItemName = i.ItemName, Description = i.Description, SortOrder = i.SortOrder,
                        IsCritical = i.IsCritical, ExpectedValue = i.ExpectedValue, UnitCode = i.UnitCode, IsRequired = i.IsRequired,
                    });
                }
                old.EffectiveTo = from.AddDays(-1);
                _unitOfWork.FoodSafetyChecklistRepository.Update(old);
                await _unitOfWork.FoodSafetyChecklistRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<FoodSafetyChecklist>.Success(copy, string.Format(ConstantHelper.Message.ChecklistNewVersionCreated, version, copy.Name ?? copy.Code, from.ToString("dd/MM/yyyy")));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " NewVersion: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<FoodSafetyChecklist> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<FoodSafetyChecklist>.Fail(message);
            }
        }
    }
}
