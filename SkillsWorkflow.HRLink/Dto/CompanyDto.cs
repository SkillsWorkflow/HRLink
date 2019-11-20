using System;

namespace SkillsWorkflow.HrLink.Dto
{
    public class CompanyDto
    {
        public Guid CompanyIntegrationId { get; set; }        
        public string Code { get; set; }
        public Guid CompanyId { get; set; }
        public DateTime? IntegratedOn { get; set; }
        public int LogRetentionDays { get; set; }
        public string AdministratorMail { get; set; }
        public string PersonalDataUrl { get; set; }
        public string JobWithCompensationUrl { get; set; }
        public string Requestor { get; set; }
        public string Password { get; set; }
        public string AccessToken { get; set; }
        public string ApiKey { get; set; }
        public string ExternalCode { get; set; }
    }
}
