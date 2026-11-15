using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IAppConfigService
    {
        public Task<IResults<AppConfigViewModel>> GetAll(AppConfigSearchRequest request);
        public Task<IResult<bool>> Save(AppConfigRequest request, string typeSave);
        public IResults<AppConfig> GetAllData();
        public Task<IResult<AppConfig>> GetById(Guid id);
    }
}
