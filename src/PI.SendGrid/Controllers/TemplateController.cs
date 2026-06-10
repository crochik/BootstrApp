using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using SendGrid;

namespace Controllers
{
    [Route("/sendgrid/v1/[controller]")]
    [Authorize("managerplus")]
    public class TemplateController : APIController
    {
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Template>), 200)]
        public async Task<IActionResult> GetTemplatesAsync([FromServices] IEntityIntegrationAdapter adapter)
        {
            var list = await adapter.GetTrunkByIdAsync(Context.EntityId.Value, IntegrationIds.SendGrid);
            var ordered = list.OrderBy(x => x.Level).ToArray();
            var data = ordered.FirstOrDefault()?.GetData<SendGridIntegration.Data>();
            var auth = ordered.FirstOrDefault()?.GetAuthentication<SendGridIntegration.Authentication>();
            if (string.IsNullOrEmpty(data?.FromEmail) || string.IsNullOrEmpty(data?.FromName) || string.IsNullOrEmpty(auth?.APIKey))
            {
                return Forbid("Integration configuration not found");
            }

            var client = new SendGridClient(auth.APIKey);
            var resp = await client.RequestAsync(
                SendGridClient.Method.GET,
                urlPath: "/templates?generations=dynamic"
            );

            var json = await resp.Body.ReadAsStringAsync();
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                return StatusCode((int)resp.StatusCode);
            }
            var templates = JsonConvert.DeserializeObject<TemplateList>(json);

            return Ok(templates.Templates);
        }

        public class TemplateList
        {
            public Template[] Templates { get; set; }
        }

        public class Template
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public TemplateVersion[] Versions { get; set; }
        }

        public class TemplateVersion
        {
            public Guid Id { get; set; }
            
            [JsonProperty("template_id")]
            public string TemplateId { get; set; }
            public bool Active { get; set; }
            public string Name { get; set; }
            
            [JsonProperty("generate_plain_content")]
            public bool GeneratePlainContext { get; set; }
            public string Subject { get; set; }
            
            [JsonProperty("updated_at")]
            public DateTime UpdatedAt { get; set; }
            public string Editor { get; set; }
        }
    }
}
