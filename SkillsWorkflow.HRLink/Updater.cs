using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SkillsWorkflow.Common;
using SkillsWorkflow.Integration.Api.Models;

namespace SkillsWorkflow.HrLink
{
    public static class Updater
    {
        public static string MyVersion => "1.0.0.3";
        public static int MyStatusId => 17;

        #region Integrator
        private static async Task<IntegratorDto> GetIntegratorAsync(HttpClient httpClient, int statusId)
        {
            var response = await httpClient.GetAsync($"api/integrators/status/{statusId}");
            response.EnsureSuccessStatusCode();
            var integratorDto = await response.Content.ReadAsJsonAsync<IntegratorDto>();
            return integratorDto;
        }

        private static IntegratorModel GetIntegratorPostModel()
        {
            var model = new IntegratorModel
            {
                Name = "HrLink",
                StatusId = MyStatusId,
                TableName = "HrLinkCompany",
                KeyColumnName = "Company",
                Active = true,
                Version = MyVersion
            };
            return model;
        }

        private static async Task<IntegratorDto> PostIntegratorAsync(HttpClient httpClient)
        {
            var postModel = GetIntegratorPostModel();
            HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(postModel), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/integrators", contentPost);
            var result = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? JsonConvert.DeserializeObject<IntegratorDto>(result) : null;
        }
        #endregion

        private static ExternalTableModel GetExternalTablePostModel()
        {
            var externalTable = new ExternalTableModel
            {
                KeyColumnName = "Company",
                Columns = new Collection<ExternalTableColumnModel>()
            };
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "AdministratorMail", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "PersonalDataUrl", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "JobWithCompensationUrl", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "Requestor", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "Password", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "AccessToken", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "ApiKey", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            externalTable.Columns.Add(new ExternalTableColumnModel { ColumnName = "Code", ColumnDataTypeId = (int)ColumnDataType.Varchar100 });
            return externalTable;
        }

        private static SkillViewModel GetSkillsWorkflowIntegrationHrLinkCompanies()
        {
            var query = @"
                select	ci.Oid as CompanyIntegrationId,
                        c.Code, 
                        x.Company as CompanyId, 
                        ci.IntegratedOn, 
                        ci.LogRetentionDays,                         
                        x.AdministratorMail,
                        x.PersonalDataUrl, 
                        x.JobWithCompensationUrl, 
                        x.Requestor, 
                        x.Password,                         
                        x.AccessToken,
                        x.ApiKey,
                        x.Code as ExternalCode
                from    Company c, CompanyIntegration ci, HrLinkCompany x
                where	x.Company = ci.Company
                        and ci.IntegrationType = 17
                        and ci.Company = c.Oid
                        and ci.Active = 1";
            return new SkillViewModel
            {
                Name = "SkillsWorkflowIntegrationHrLinkCompanies",
                Query = query,
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow
            };
        }



        public static async Task UpdateAsync(ApiDto apiDto, MailDto mailDto, ContextDto contextDto)
        {
            try
            {
                var httpClient = HttpClientHelper.Get(apiDto);
                var integratorDto = await GetIntegratorAsync(httpClient, MyStatusId);
                if (integratorDto != null && integratorDto.Version == MyVersion) return;
                await ExternalTableHelper.PostAsync(httpClient, "HrLinkCompany", GetExternalTablePostModel());
                await DocumentUserFieldHelper.PostAsync(httpClient, "Skill.Module.BusinessObjects.User", "HRLinkId", (int)ColumnDataType.Varchar50);
                await DatabaseHelper.RecreateViewsAsync(httpClient, GetSkillsWorkflowIntegrationHrLinkCompanies());                
                if (!await DatabaseHelper.ExistsViewAsync(apiDto, "SkillsWorkflowIntegrationHrLinkCompanies")) throw new Exception("View SkillsWorkflowIntegrationHrLinkCompanies not created.");                
                await PostIntegratorAsync(httpClient);
            }
            catch (Exception exception)
            {
                var groupId = contextDto.TenantId.ToString() + "-" + Updater.MyStatusId.ToString();
                await MailHelper.SendEmailAsync(mailDto, "Error updating HrLink structure.", MailHelper.GetBodyTextOf(contextDto, exception.InnerException?.Message ?? exception.Message), mailDto.AdministratorMail, null, true, groupId);
            }
        }
    }
}
