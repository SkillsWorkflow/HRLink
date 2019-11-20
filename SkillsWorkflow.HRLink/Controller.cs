using Newtonsoft.Json;
using SkillsWorkflow.Common;
using SkillsWorkflow.HrLink.Dto;
using SkillsWorkflow.HrLink.Helpers;
using SkillsWorkflow.HrLink.Interfaces;
using SkillsWorkflow.Integration.Api.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkillsWorkflow.HrLink
{
    public class Controller
    {
        private List<ApiDto> Tenants { get; }
        private MailDto MailDto { get; }
        private readonly ICompanyHelper _companyHelper;
        private readonly IUserHelper _userHelper;

        public Controller(List<ApiDto> tenants, MailDto mailDto)
        {
            Tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
            MailDto = mailDto ?? throw new ArgumentNullException(nameof(mailDto));
            _companyHelper = new CompanyHelper();
            _userHelper = new UserHelper();
        }

        private async Task<List<TenantCompaniesDto>> GetTenantCompanies()
        {
            var tenantCompanies = new List<TenantCompaniesDto>();
            foreach (var tenant in Tenants)
            {
                var apiDto = new ApiDto { Url = tenant.Url, Id = tenant.Id, Secret = tenant.Secret };
                var companies = await _companyHelper.GetAsync(apiDto);
                if (companies == null || companies.Count == 0) continue;
                var tenantCompaniesDto = new TenantCompaniesDto { Api = apiDto, Companies = companies };
                tenantCompanies.Add(tenantCompaniesDto);
            }
            return tenantCompanies;
        }

        private async Task DeleteLogAsync(List<TenantCompaniesDto> tenantCompanies)
        {
            foreach (var tenantCompany in tenantCompanies)
                foreach (var company in tenantCompany.Companies)
                    await ApiHelper.DeleteLogAsync(tenantCompany.Api, company.CompanyIntegrationId);
        }

        private async Task ImportUsersAsync(List<TenantCompaniesDto> tenantCompanies, List<MailBodyDto> mailBodyList)
        {
            Console.WriteLine("Importing Users.");
            foreach (var tenantCompany in tenantCompanies)
                foreach(var company in tenantCompany.Companies)
                {
                    Console.WriteLine("GetPersonalDataAsync from " + company.Code);
                    var personalData = await _userHelper.GetPersonalDataAsync(tenantCompany.Api, company, mailBodyList);
                    Console.WriteLine("GetJobDataAsync from " + company.Code);
                    var jobData = await _userHelper.GetJobDataAsync(tenantCompany.Api, company, mailBodyList);
                    Console.WriteLine("ImportAsync users from " + company.Code);
                    await _userHelper.ImportAsync(tenantCompany.Api, company, personalData, jobData, mailBodyList);
                }
        }

        private async Task SendEmailAsync(List<MailBodyDto> mailBodyList, ContextDto context, string companyCode, string companyAdministratorMail)
        {
            var groupId = context.TenantId.ToString() + "-" + Updater.MyStatusId.ToString();
            var companyAdministratorMailBody = mailBodyList.Where(mbl => mbl.CompanyCode == companyCode && string.IsNullOrEmpty(mbl.NotifyUserMail)).ToList();
            if (companyAdministratorMailBody.Any() && !string.IsNullOrEmpty(companyAdministratorMail))
                await MailHelper.SendEmailAsync(MailDto, "Error executing HrLink job.", MailHelper.GetBodyTextOf(context, companyAdministratorMailBody), companyAdministratorMail, null, true, groupId);
            var notifyUserMails = mailBodyList.Where(mbl => mbl.CompanyCode == companyCode && !string.IsNullOrEmpty(mbl.NotifyUserMail)).Select(mbl => mbl.NotifyUserMail).Distinct();
            foreach (var notifyUserMail in notifyUserMails)
            {
                var notifyUserMailBody = mailBodyList.Where(mbl => mbl.CompanyCode == companyCode && mbl.NotifyUserMail == notifyUserMail).ToList();
                await MailHelper.SendEmailAsync(MailDto, "HrLink", MailHelper.GetBodyTextOf(notifyUserMailBody), notifyUserMail, null, true, groupId);
            }
        }

        public async Task ExecuteAsync()
        {
            Console.WriteLine("<Controller:Execute> Starting");
            try
            {
                var datetimeNow = DateTime.UtcNow;
                var tenantCompanies = await GetTenantCompanies();
                if (tenantCompanies.Count == 0) return;
                var mailBodyList = new List<MailBodyDto>();
                Console.WriteLine("<Controller:Execute> Delete Log");
                await DeleteLogAsync(tenantCompanies);                
                await ImportUsersAsync(tenantCompanies, mailBodyList);
                Console.WriteLine("<Controller:Execute> Update Integrated On");
                foreach (var tenantCompany in tenantCompanies)
                    foreach (var company in tenantCompany.Companies)
                        await CompanyIntegrationHelper.UpdateAsync(tenantCompany.Api, company.CompanyIntegrationId, datetimeNow);
                foreach (var tenantCompany in tenantCompanies)
                {
                    var contextDto = await ApiHelper.GetContextDtoAsync(tenantCompany.Api);
                    foreach (var company in tenantCompany.Companies)
                    {
                        var administratorMail = string.IsNullOrEmpty(company.AdministratorMail) ? MailDto.AdministratorMail : company.AdministratorMail;
                        await SendEmailAsync(mailBodyList, contextDto, company.Code, administratorMail);
                    }
                }
            }
            catch (Exception exception)
            {
                var contextDto = await ApiHelper.GetContextDtoAsync(Tenants.First());
                var groupId = contextDto.TenantId.ToString() + "-" + Updater.MyStatusId.ToString();
                await MailHelper.SendEmailAsync(MailDto, "Error executing HrLink job.", MailHelper.GetBodyTextOf(contextDto, exception.InnerException?.Message ?? exception.Message), MailDto.AdministratorMail, null, true, groupId);
                throw;
            }
            Console.WriteLine("<Controller:Execute> Ending");
        }

        public async Task ExecuteTestAsync()
        {
            Console.WriteLine("<Controller:Execute> Starting");
            var mailBodyList = new List<MailBodyDto>();
            var tenantCompanies = await GetTenantCompanies();
            if (tenantCompanies.Count == 0) return;
            PersonalDataResponse personalData = null;
            using (StreamReader r = new StreamReader("C:\\Test\\HrLink\\Employee_response.json"))
            {
                string json = r.ReadToEnd();
                personalData = JsonConvert.DeserializeObject<PersonalDataResponse>(json);
                //var persons = personalDataResponse.SoapenvEnvelope.Body.ApiVersionPersons.Persons;
            }
            JobDataResponse jobData = null;
            using (StreamReader r = new StreamReader("C:\\Test\\HRLink\\Job_response.json"))
            {
                string json = r.ReadToEnd();
                jobData = JsonConvert.DeserializeObject<JobDataResponse>(json);
                //var jobs = jDataResponse.SoapenvEnvelope.Body.ApiVersionJobs.JobDataWithCompList;
            }
            var defaultTenant = tenantCompanies.First();
            var defaultCompany = tenantCompanies.First().Companies.First();
            var imported = await _userHelper.ImportAsync(defaultTenant.Api, defaultCompany, personalData, jobData, mailBodyList);
            Console.WriteLine("<Controller:Execute> Ending");
        }
    }
}
