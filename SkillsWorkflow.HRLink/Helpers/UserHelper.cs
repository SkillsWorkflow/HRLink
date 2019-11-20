using Newtonsoft.Json;
using SkillsWorkflow.Common;
using SkillsWorkflow.HrLink.Dto;
using SkillsWorkflow.HrLink.Interfaces;
using SkillsWorkflow.Integration.Api.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SkillsWorkflow.HrLink.Helpers
{
    public class UserHelper : IUserHelper
    {
        private static async Task<DepartmentDto> GetDepartmentAsync(HttpClient httpClient, Guid companyId, string externalId)
        {
            var response = await httpClient.GetAsync($"/api/companies/{companyId}/departments/external?externalId={HttpUtility.UrlEncode(externalId)}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode == HttpStatusCode.BadRequest) return null;
            return await response.Content.ReadAsJsonAsync<DepartmentDto>();
        }

        private static async Task<EmployeeDto> SaveEmployeeAsync(HttpClient httpClient, Dto.CompanyDto company, string externalId, string userName, bool active, string progressMessage, List<MailBodyDto> mailBodyList)
        {
            EmployeeModel model = new EmployeeModel
            {
                CompanyId = company.CompanyId,
                CompanyCode = company.Code,
                ExternalId = externalId,
                Name = userName,
                IsActive = active
            };
            HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/employees", contentPost);
            if (response.IsSuccessStatusCode)
            {
                var employeeDto = await response.Content.ReadAsJsonAsync<EmployeeDto>();
                await ApiHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "Employee", userName, employeeDto.Action, progressMessage, JsonConvert.SerializeObject(model));
                return employeeDto;
            }
            var errorMessage = response.Content.ReadAsStringAsync().Result;
            await ApiHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "Employee", userName, errorMessage, progressMessage, JsonConvert.SerializeObject(model));
            return null;
        }

        private static async Task<UserTypologyDto> GetUserTypologyAsync(HttpClient httpClient, Guid departmentId, string externalId)
        {
            var response = await httpClient.GetAsync($"/api/departments/{departmentId}/usertypologies/external?externalId={HttpUtility.UrlEncode(externalId)}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsJsonAsync<UserTypologyDto>();
        }

        private static async Task<bool> SaveUserTypologyGroupAsync(HttpClient httpClient, UserTypologyGroupModel model, string progressMessage)
        {            
            HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/usertypologygroups", contentPost);
            if (response.IsSuccessStatusCode)
            {
                var userTypologyGroupDto = await response.Content.ReadAsJsonAsync<UserTypologyGroupDto>();
                await ApiHelper.SaveLogAsync(httpClient, model.CompanyCode, model.CompanyId, Updater.MyStatusId, "UserTypologyGroup", model.Name, userTypologyGroupDto.Action, progressMessage, JsonConvert.SerializeObject(model));
                return true;
            }
            var errorMessage = response.Content.ReadAsStringAsync().Result;
            await ApiHelper.SaveLogAsync(httpClient, model.CompanyCode, model.CompanyId, Updater.MyStatusId, "UserTypologyGroup", model.Name, errorMessage, progressMessage, JsonConvert.SerializeObject(model));
            return response.IsSuccessStatusCode;
        }

        private static async Task<bool> SaveUserTypologyAsync(HttpClient httpClient, UserTypologyModel model, string progressMessage)
        {
            HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/usertypologies", contentPost);
            if (response.IsSuccessStatusCode)
            {
                var userTypologyDto = await response.Content.ReadAsJsonAsync<UserTypologyDto>();
                await ApiHelper.SaveLogAsync(httpClient, model.CompanyCode, model.CompanyId, Updater.MyStatusId, "UserTypology", model.Name, userTypologyDto.Action, progressMessage, JsonConvert.SerializeObject(model));
                return true;
            }
            var errorMessage = response.Content.ReadAsStringAsync().Result;
            await ApiHelper.SaveLogAsync(httpClient, model.CompanyCode, model.CompanyId, Updater.MyStatusId, "UserTypology", model.Name, errorMessage, progressMessage, JsonConvert.SerializeObject(model));
            return response.IsSuccessStatusCode;
        }

        private static async Task<bool> SaveUserTypologyAsync(HttpClient httpClient, Dto.CompanyDto company, Guid departmentId, JobDataWithComp job, string progressMessage)
        {
            var userTypologyName = job.JobFunction.JobFunctionJobFunction.Text;
            var userTypology = await GetUserTypologyAsync(httpClient, departmentId, job.JobFunction.JobFunctionJobFunction.Text);
            if (userTypology != null)
                return true;
            var userTypologyGroupModel = new UserTypologyGroupModel
            {
                CompanyId = company.CompanyId,
                Name = userTypologyName,
                Active = true
            };
            var saveUserTypologyGroup = await SaveUserTypologyGroupAsync(httpClient, userTypologyGroupModel, progressMessage);            
            if (!saveUserTypologyGroup)
                return false;
            UserTypologyModel model = new UserTypologyModel
            {
                CompanyId = company.CompanyId,
                Name = userTypologyName,
                ExternalId = userTypologyName,
                DepartmentId = departmentId,
                UserTypologyGroupName = userTypologyName,
                Active = true,
            };
            var saveUserTypology = await SaveUserTypologyAsync(httpClient, model, progressMessage);            
            return saveUserTypology;
        }

        private static async Task<UserDto> SaveUserAsync(HttpClient httpClient, Dto.CompanyDto company, Guid userId, string externalId, string userName, bool isActive, Person person, JobDataWithComp job, Guid employeeId, string progressMessage, List<MailBodyDto> mailBodyList)
        {
            UserModel model = new UserModel
            {
                Id = userId,
                Name = userName,
                UserName = userName,
                ExternalId = externalId,
                CompanyId = company.CompanyId,
                CompanyCode = company.Code,
                EmployeeId = employeeId,
                DepartmentExternalId = job.Department.Deptid.Text,
                TypologyExternalId = job.JobFunction.JobFunctionJobFunction.Text,
                Mail = person.Email.EmailAddr.Text,
                Phone = person.PersonalPhone.Phone.Text,
                IsActive = isActive
            };            
            if (!string.IsNullOrEmpty(job.Job.HireDt.Text))
                model.HireDate = Convert.ToDateTime(job.Job.HireDt.Text);
            //public string SsoUserName { get; set; }
            //public bool SsoUserNameUpdatable { get; set; }
            //public DateTime? ExpirationDate { get; set; }
            //public double RequiredWeeklyHours { get; set; }
            //public double RequiredHours { get; set; }
            //public Guid ResponsibleId { get; set; }
            //public Guid UserTypeId { get; set; }
            //public double WeeklyOvertimeThresholdHours { get; set; }
            //public bool ExternalNumberUpdatable { get; set; }
            //public string ExternalNumber { get; set; }
            HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/users", contentPost);
            if (response.IsSuccessStatusCode)
            {
                var savedUserDto = await response.Content.ReadAsJsonAsync<UserDto>();
                await ApiHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "User", model.UserName, savedUserDto.Action, progressMessage, JsonConvert.SerializeObject(model));
                return savedUserDto;
            }
            var errorMessage = response.Content.ReadAsStringAsync().Result;
            await ApiHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "User", model.UserName, errorMessage, progressMessage, JsonConvert.SerializeObject(model));
            return null;
        }

        private async Task<List<DocumentUserFieldValueDto>> GetUserIdByHrLinkIdAsync(HttpClient httpClient, string userExternalId)
        {
            var columnName = HttpUtility.UrlEncode("HRLinkId");
            var response = await httpClient.GetAsync($"/api/documentUserFieldValues/values?documentTypeName=User&columnName={columnName}&value={HttpUtility.UrlEncode(userExternalId)}&valueType=" + (int)ColumnDataType.Varchar50);
            if (response.StatusCode == HttpStatusCode.NotFound) return new List<DocumentUserFieldValueDto>();
            if (response.StatusCode == HttpStatusCode.BadRequest) return new List<DocumentUserFieldValueDto>();
            return await response.Content.ReadAsJsonAsync<List<DocumentUserFieldValueDto>>();
        }

        private async Task<UserDto> GetUserAsync(HttpClient httpClient, Guid userId)
        {
            var response = await httpClient.GetAsync($"/api/users/{userId}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode == HttpStatusCode.BadRequest) return null;
            return await response.Content.ReadAsJsonAsync<UserDto>();
        }

        private async Task SaveDocumentUserFieldValueAsync(HttpClient httpClient, Dto.CompanyDto companyDto, DocumentUserFieldValueBatchPutModel model, string progressMessage)
        {
            HttpContent contentPost = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync("/api/documentUserFieldValues", contentPost);
            if (response.IsSuccessStatusCode)
            {
                await ApiHelper.SaveLogAsync(httpClient, companyDto.Code, companyDto.CompanyId, Updater.MyStatusId, "Document User Fields", "", "-- Updated --", progressMessage, JsonConvert.SerializeObject(model));
                return;
            }
            await ApiHelper.SaveLogAsync(httpClient, companyDto.Code, companyDto.CompanyId, Updater.MyStatusId, "Document User Fields", "", response.Content.ReadAsStringAsync().Result, progressMessage, JsonConvert.SerializeObject(model));
        }


        public async Task<JobDataResponse> GetJobDataAsync(ApiDto apiDto, Dto.CompanyDto company, List<MailBodyDto> mailBodyList)
        {
            var httpClient = HttpClientHelper.Get(apiDto);
            string body = "";
            string uriString = company.JobWithCompensationUrl + $"?REQUESTOR={HttpUtility.UrlEncode(company.Requestor)}&PASSWORD={HttpUtility.UrlEncode(company.Password)}&COMPANY={HttpUtility.UrlEncode(company.ExternalCode)}";
            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = new HttpMethod("POST"),
                RequestUri = new Uri(uriString),
                Content = new StringContent(body)
            };
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {company.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("apikey", company.ApiKey);
            httpClient.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsJsonAsync<JobDataResponse>();
            var message = await response.Content.ReadAsStringAsync();
            mailBodyList.Add(new MailBodyDto { CompanyCode = company.Code, Message = message });            
            return null;
        }

        public async Task<PersonalDataResponse> GetPersonalDataAsync(ApiDto apiDto, Dto.CompanyDto company, List<MailBodyDto> mailBodyList)
        {
            var httpClient = HttpClientHelper.Get(apiDto);
            string body = "";
            string uriString = company.PersonalDataUrl + $"?REQUESTOR={HttpUtility.UrlEncode(company.Requestor)}&PASSWORD={HttpUtility.UrlEncode(company.Password)}&COMPANY={HttpUtility.UrlEncode(company.ExternalCode)}";
            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = new HttpMethod("POST"),
                RequestUri = new Uri(uriString),
                Content = new StringContent(body)
            };
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {company.AccessToken}");
            httpClient.DefaultRequestHeaders.Add("apikey", company.ApiKey);
            httpClient.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsJsonAsync<PersonalDataResponse>();
            var message = await response.Content.ReadAsStringAsync();
            mailBodyList.Add(new MailBodyDto { CompanyCode = company.Code, Message = message });
            return null;
        }

        public async Task<bool> ImportAsync(ApiDto api, Dto.CompanyDto company, PersonalDataResponse personalData, JobDataResponse jobData, List<MailBodyDto> mailBodyList)
        {
            var persons = personalData == null ? new List<Person>() : personalData.SoapenvEnvelope.Body.ApiVersionPersons.Persons.ToList();            
            var jobs = jobData == null ? new List<JobDataWithComp>() : jobData.SoapenvEnvelope.Body.ApiVersionJobs.JobDataWithCompList.ToList();
            int count = 1;
            var httpClient = HttpClientHelper.Get(api);
            var imported = true;
            foreach (var person in persons)
            {
                var progressMessage = $"Importing {count++} of {persons.Count}.";
                var userId = Guid.Empty;
                var externalId = person.Emplid.Text.ToString();
                var userName = person.SubNames.NameDisplay.Text;
                var job = jobs.FirstOrDefault(j => j.Job.Emplid.Text.ToString(CultureInfo.InvariantCulture) == externalId);
                if(job == null)
                {
                    await LogHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "User", userName, $"-- Not Imported - Job data must exists for employeeid {externalId} --", progressMessage, JsonConvert.SerializeObject(person));
                    imported = false;
                    continue;
                }
                var department = await GetDepartmentAsync(httpClient, company.CompanyId, job.Department.Deptid.Text);
                if (department == null)
                {
                    await ApiHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "User", userName, $"Department does not exists for external id {job.Department.Deptid.Text}.", progressMessage, JsonConvert.SerializeObject(person));
                    continue;
                }
                var isActive = job.Job.EmplStatus.Text == "A";
                var documentUserFieldValueDtos = await GetUserIdByHrLinkIdAsync(httpClient, person.Emplid.Text.ToString());
                if (documentUserFieldValueDtos.Count > 1)
                {
                    await ApiHelper.SaveLogAsync(httpClient, company.Code, company.CompanyId, Updater.MyStatusId, "User", userName, $"Exists several users with {person.Emplid.Text} HRLinkID.", progressMessage, JsonConvert.SerializeObject(person));
                    return false;
                }
                if (documentUserFieldValueDtos.Count == 1)
                {
                    var user = await GetUserAsync(httpClient, documentUserFieldValueDtos[0].DocumentId);
                    externalId = user.ExternalId;
                    userId = user.Id;
                }
                else
                    externalId = (externalId.Length == 10 && externalId.Substring(2, 2) == "00") ? externalId.Remove(2, 2) : externalId;
                var employeeDto = await SaveEmployeeAsync(httpClient, company, externalId, userName, isActive, progressMessage, mailBodyList);
                if (employeeDto == null)
                {
                    imported = false;
                    continue;
                }
                var savedUserTypology = await SaveUserTypologyAsync(httpClient, company, department.Id, job, progressMessage);
                if (!savedUserTypology)
                {
                    imported = false;
                    continue;
                }
                var savedUserDto = await SaveUserAsync(httpClient, company, userId, externalId, userName, isActive, person, job, employeeDto.Id, progressMessage, mailBodyList);
                if (savedUserDto == null)
                {
                    imported = false;
                    continue;
                }
                var documentUserFieldValueBatchPutModel = new DocumentUserFieldValueBatchPutModel
                {
                    DocumentId = savedUserDto.Id,
                    DocumentTypeName = "User",
                    DocumentUserFieldValues = new List<DocumentUserFieldValue>()
                };
                documentUserFieldValueBatchPutModel.DocumentUserFieldValues.Add(new DocumentUserFieldValue { ColumnName = "HRLinkId", Value = person.Emplid.Text.ToString(), ColumnDataTypeId = (int)ColumnDataType.Varchar50 });
                await SaveDocumentUserFieldValueAsync(httpClient, company, documentUserFieldValueBatchPutModel, progressMessage);
            }
            return imported;
        }
    }
}
