using SkillsWorkflow.Common;
using SkillsWorkflow.HrLink.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkillsWorkflow.HrLink.Interfaces
{
    public interface IUserHelper
    {
        Task<PersonalDataResponse> GetPersonalDataAsync(ApiDto apiDto, CompanyDto company, List<MailBodyDto> mailBodyList);
        Task<JobDataResponse> GetJobDataAsync(ApiDto apiDto, CompanyDto company, List<MailBodyDto> mailBodyList);
        Task<bool> ImportAsync(ApiDto api, Dto.CompanyDto company, PersonalDataResponse personalData, JobDataResponse jobData, List<MailBodyDto> mailBodyList);
    }
}
