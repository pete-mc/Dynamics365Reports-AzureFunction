#r "Newtonsoft.Json"

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using System.IO;
using OpenHtmlToPdf; 

// updated
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, ExecutionContext context)
{
    log.Info("C# HTTP trigger function processed a request.");

    // parse query parameter and connect to CRM 
    string crmId = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "crmId", true) == 0).Value;
    string crmOrg = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "crmorg", true) == 0).Value;
    string apiKey = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "apikey", true) == 0).Value;
    bool Attachment = true;
    if (crmId == null){
        log.Info("Using Request Body");
        string bodyContent = await req.Content.ReadAsStringAsync();
        Dictionary<string, string> htmlAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyContent);
        crmId = htmlAttributes["crmId"];
        crmOrg = htmlAttributes["crmorg"];
        apiKey = htmlAttributes["apikey"];
        Attachment = false;
    } else
    {
        log.Info("Using Query");
    }
    //check if key is correct
    if (apiKey != "dfgfdgfdgfdgavasdsadvx979amdb2349sbkmb435skvd430") { 
        var resBad = new HttpResponseMessage(HttpStatusCode.Forbidden);
        return resBad;
    } 
    IServiceManagement<IOrganizationService> orgServiceManagement = ServiceConfigurationFactory.CreateManagement<IOrganizationService>(new Uri("https://"+crmOrg+".api.crm6.dynamics.com/XRMServices/2011/Organization.svc"));    
    AuthenticationCredentials authCredentials = new AuthenticationCredentials();
    authCredentials.ClientCredentials.UserName.UserName = "";
    authCredentials.ClientCredentials.UserName.Password = "";
    AuthenticationCredentials tokenCredentials = orgServiceManagement.Authenticate(authCredentials);
    OrganizationServiceProxy organizationProxy = new OrganizationServiceProxy(orgServiceManagement, tokenCredentials.SecurityTokenResponse);
    // import HTML templates
    string templateFile = ""; //HTML URL.
    string htmlTemplate = string.Join(" ", File.ReadAllLines(templateFile,System.Text.Encoding.UTF8));
    string SnippetTemplate(string FileName)
    {
        string fullFileName = Path.Combine(context.FunctionAppDirectory,"VincentianAssistanceReport", FileName);
        return string.Join(" ", File.ReadAllLines(fullFileName,System.Text.Encoding.UTF8));
 
    }

    // Get CRM data
    var CRMColumnSet = new ColumnSet(true);
    var CRMGuid = new Guid(crmId);
    var CRMRecord = organizationProxy.Retrieve("name of entity",CRMGuid,CRMColumnSet);

    DataCollection<Entity> GetRelatedEntity(string retriveEntity, string filterAttribute, Guid filterValue,string orderField, OrderType orderType)
    {
        QueryExpression tempQuery = new QueryExpression
        {
            EntityName = retriveEntity, ColumnSet = new ColumnSet(true), 
            Criteria = new FilterExpression {
                Conditions = {
                    new ConditionExpression {
                        AttributeName = filterAttribute,
                        Operator = ConditionOperator.Equal,
                        Values = { filterValue }
                    }
                }
            },                
            Orders = {
                    new OrderExpression {
                        AttributeName = orderField,
                        OrderType = orderType
                    }
            }
        };
        DataCollection<Entity> tempDCEntity = organizationProxy.RetrieveMultiple(tempQuery).Entities;
        return tempDCEntity;
    }
    string getEntValue(string CRMKey, Entity ent)
    {
        return ent.FormattedValues.ContainsKey(CRMKey) ? ent.FormattedValues[CRMKey].ToString() : ent.Attributes.ContainsKey(CRMKey) ? ent.Attributes[CRMKey].ToString() : "";
    }
    string removeYearFromDate(string dateToProcess){
        var dateArray = dateToProcess.Split('/');
        if (dateArray.Length != 3) return "";
        return dateArray[0]+'/'+dateArray[1]+'/'+dateArray[2].Substring(2,2);
    }

    htmlTemplate = htmlTemplate.Replace("stuff",PastAssistanceValue)        ;
    
    // PDF Creation
    log.Info("Processing PDF Request");
    var pdf = Pdf.From(htmlTemplate).Content();
    var res = new HttpResponseMessage(HttpStatusCode.OK);
        res.Content = new ByteArrayContent(pdf);
        res.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        if (Attachment)
        {
            res.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
        }else
        {
            res.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline");
        }
    return res;
}

