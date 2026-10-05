using System;
using System.Collections.Generic;
using System.Net.Mail;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Maintenance;
using ForteMove.Models.Common;
using ForteMove.Models.Maintenance;
namespace ForteMove.Business.Services
{
    public sealed class RepairProviderService
    {
        private readonly IRepairProviderRepository repository;
        public RepairProviderService(IRepairProviderRepository repository){if(repository==null)throw new ArgumentNullException("repository");this.repository=repository;}
        public IList<RepairProvider> GetProviders(){return repository.GetProviders();}
        public RepairProvider GetProvider(long id){return repository.GetProvider(id);}
        public ServiceResult<long> SaveProvider(SaveRepairProviderRequest r,long actor)
        {
            if(r==null)return ServiceResult<long>.Failure("","Enter a repair provider.");
            r.ProviderName=(r.ProviderName??"").Trim();r.AreaDescription=(r.AreaDescription??"").Trim();r.Phone=Trim(r.Phone);r.Email=Trim(r.Email);
            var e=Validate(r);
            if(e.Count>0)return ServiceResult<long>.Failure(e);
            try{return ServiceResult<long>.Success(repository.SaveProvider(r,actor));}catch(MaintenancePersistenceException x){return ServiceResult<long>.Failure("",x.Message);}
        }
        public static IList<ValidationError> Validate(SaveRepairProviderRequest r){return MaintenancePolicy.ValidateProvider(r);}
        private static string Trim(string s){return string.IsNullOrWhiteSpace(s)?null:s.Trim();}
    }
}
