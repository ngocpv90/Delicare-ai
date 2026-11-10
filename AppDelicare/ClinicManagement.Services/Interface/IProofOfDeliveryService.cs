using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProofOfDeliveryService
    {
        public Task<IResults<ProofOfDeliveryViewModel>> GetAll(ProofOfDeliverySearchRequest request);
        public Task<IResult<bool>> Save(ProofOfDeliveryRequest request, string typeSave);
        public IResults<ProofOfDelivery> GetAllData();
        public Task<IResult<ProofOfDelivery>> GetById(Guid id);
    }
}
