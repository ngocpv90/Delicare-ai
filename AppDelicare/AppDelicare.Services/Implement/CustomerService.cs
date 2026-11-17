using AppDelicare.Data.Share;
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
    public class CustomerService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock) : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<CustomerViewModel>> GetAll(CustomerSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                dynamicParameters.Add("@TaxCode", request.TaxCode);
                dynamicParameters.Add("@Phone", request.Phone);
                dynamicParameters.Add("@Email", request.Email);
                //dynamicParameters.Add("@Address", request.Address);
                dynamicParameters.Add("@CustomerType", request.CustomerType);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<CustomerViewModel>("uspCustomer_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<CustomerViewModel>
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

        public IResults<Customer> GetAllData()
        {
            try
            {
                var list = _unitOfWork.CustomerRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Customer>
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

        public async Task<IResult<bool>> Save(CustomerRequest request, string typeSave)
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

        private async Task<string> Insert(CustomerRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var checkDuplicate = _unitOfWork.CustomerRepository.GetSingle(x => x.Code == request.Code && !x.IsDeleted);
            if (checkDuplicate != null)
            {
                return string.Format(ConstantHelper.Message.CodeExist, request.Code);
            }
            var insert = _mapper.Map<Customer>(request);
            _unitOfWork.CustomerRepository.Add(insert);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(CustomerRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.CustomerRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var checkDuplicate = _unitOfWork.CustomerRepository.GetSingle(x => x.Id != request.Id && x.Code == request.Code && !x.IsDeleted);
            if (checkDuplicate != null)
            {
                return string.Format(ConstantHelper.Message.CodeExist, request.Code);
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.CustomerRepository.Update(update);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Customer), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.CustomerRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.CustomerRepository.Update(delete);
            await _unitOfWork.CustomerRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Customer>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.CustomerRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Customer>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Customer>
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

        /// <summary>T45R (spec 4.1): CIF không trùng.</summary>
        private async Task<string> CheckRules(CustomerRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.CIF)
                && await _unitOfWork.CustomerRepository.GetAll().AnyAsync(x => x.CIF == request.CIF && x.Id != request.Id && !x.IsDeleted))
            {
                return string.Format(ConstantHelper.Message.CifExist, request.CIF);
            }
            return string.Empty;
        }

        /// <summary>Đơn đã giao xong – dùng tính số suất trung bình 7 ngày.</summary>
        private static readonly string[] CompletedOrderStatuses = ["Delivered", "Reconciled"];

        /// <summary>T45R (spec 4.1): hồ sơ khách hàng 360° – liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI.</summary>
        public async Task<IResult<CustomerOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var customer = await _unitOfWork.CustomerRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (customer == null)
                {
                    return Result<CustomerOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                var contracts = await db.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).OrderByDescending(x => x.StartDate).ToListAsync();
                var contractIds = contracts.Select(x => x.Id).ToList();
                var meals = await db.ContractMeal.AsNoTracking().Where(m => !m.IsDeleted && contractIds.Contains(m.ContractId)).ToListAsync();
                var orders = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.CustomerId == id);
                DateTime since = clock.Today /* Rà soát BE: giờ nghiệp vụ VN */.AddDays(-7);
                decimal meals7d = await orders.Where(o => o.MealDate >= since && CompletedOrderStatuses.Contains(o.Status!)).SumAsync(o => o.TotalQuantity) ?? 0m;
                var model = new CustomerOverviewModel
                {
                    Customer = customer,
                    ListContact = await db.CustomerContact.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(),
                    ListLocation = await db.CustomerDeliveryLocation.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).ToListAsync(),
                    ListContract = contracts.Select(x => new ContractOverviewModel
                    {
                        Id = x.Id, ContractNumber = x.ContractNumber, Name = x.Name, StartDate = x.StartDate, EndDate = x.EndDate, Status = x.Status,
                        ContractValue = x.ContractValue, ListMeal = meals.Where(m => m.ContractId == x.Id).ToList(),
                    }).ToList(),
                    ListRecentOrder = await orders.OrderByDescending(o => o.MealDate).Take(15).Select(o => new OrderBriefModel
                    {
                        Id = o.Id, OrderNumber = o.OrderNumber, MealDate = o.MealDate, DeliveryDate = o.DeliveryDate,
                        TotalQuantity = o.TotalQuantity, TotalAmount = o.TotalAmount, Status = o.Status,
                    }).ToListAsync(),
                    OrderCount = await orders.CountAsync(),
                    ReconciledRevenue = await (from r in db.OrderReconciliation.AsNoTracking()
                                               join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                                               where !r.IsDeleted && !o.IsDeleted && o.CustomerId == id
                                               select r.FinalAmount).SumAsync() ?? 0m,
                    ComplaintCount = await db.CustomerComplaint.AsNoTracking().CountAsync(x => !x.IsDeleted && x.CustomerId == id),
                    AvgMealsPerDay7d = Math.Round(meals7d / 7m, 1),
                };
                return Result<CustomerOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }
    }
}
