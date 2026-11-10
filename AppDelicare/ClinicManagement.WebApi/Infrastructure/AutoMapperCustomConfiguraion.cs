using AutoMapper;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.WebApi.Infrastructure
{
    public class AutoMapperCustomConfiguraion : Profile
    {
        public AutoMapperCustomConfiguraion()
        {
            CreateMap<TestDemo, TestDemoModel>();
            CreateMap<TestDemoCustomRequest, TestDemo>();
            CreateMap<PersonCustomRequest, Person>();
            CreateMap<PatientCustomRequest, Patient>();
        }
    }
}
