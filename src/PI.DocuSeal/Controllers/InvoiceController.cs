// using Microsoft.AspNetCore.Mvc;
//
// namespace PI.DocuSeal.Controllers;
//
// [ApiController]
// [Route("api/[controller]")]
// public class InvoiceController : ControllerBase
// {
//     private readonly IInvoiceService _invoiceService;
//     private readonly IDocuSealService _docuSealService;
//
//     public InvoiceController(IInvoiceService invoiceService, IDocuSealService docuSealService)
//     {
//         _invoiceService = invoiceService;
//         _docuSealService = docuSealService;
//     }
//
//     /// <summary>
//     /// Generate an invoice HTML using the specified template engine
//     /// </summary>
//     /// <param name="request">Invoice request containing all invoice details and template engine</param>
//     /// <returns>Generated HTML invoice</returns>
//     [HttpPost("generate")]
//     public async Task<IActionResult> GenerateInvoice([FromBody] InvoiceRequest request)
//     {
//         try
//         {
//             if (request == null)
//             {
//                 return BadRequest("Invoice request cannot be null");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
//             {
//                 return BadRequest("Invoice number is required");
//             }
//
//             if (request.Items == null || !request.Items.Any())
//             {
//                 return BadRequest("At least one invoice item is required");
//             }
//
//             var invoice = MapToInvoice(request);
//             var html = await _invoiceService.GenerateInvoiceHtmlAsync(invoice, request.TemplateEngine);
//             
//             return Content(html, "text/html");
//         }
//         catch (NotSupportedException ex)
//         {
//             return BadRequest(new { error = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while generating the invoice", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Generate an invoice HTML using the specified template engine and return as downloadable file
//     /// </summary>
//     /// <param name="request">Invoice request containing all invoice details and template engine</param>
//     /// <returns>HTML file download</returns>
//     [HttpPost("generate/download")]
//     public async Task<IActionResult> GenerateInvoiceDownload([FromBody] InvoiceRequest request)
//     {
//         try
//         {
//             if (request == null)
//             {
//                 return BadRequest("Invoice request cannot be null");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
//             {
//                 return BadRequest("Invoice number is required");
//             }
//
//             if (request.Items == null || !request.Items.Any())
//             {
//                 return BadRequest("At least one invoice item is required");
//             }
//
//             var invoice = MapToInvoice(request);
//             var html = await _invoiceService.GenerateInvoiceHtmlAsync(invoice, request.TemplateEngine);
//             var bytes = System.Text.Encoding.UTF8.GetBytes(html);
//             
//             return File(bytes, "text/html", $"Invoice_{request.InvoiceNumber}_{request.TemplateEngine}.html");
//         }
//         catch (NotSupportedException ex)
//         {
//             return BadRequest(new { error = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while generating the invoice", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Generate an invoice HTML using a custom template with the specified template engine
//     /// </summary>
//     /// <param name="request">Request containing invoice data, template engine, and custom template</param>
//     /// <returns>Generated HTML invoice using the custom template</returns>
//     [HttpPost("generate/custom-template")]
//     public async Task<IActionResult> GenerateInvoiceWithCustomTemplate([FromBody] CustomTemplateInvoiceRequest request)
//     {
//         try
//         {
//             if (request == null)
//             {
//                 return BadRequest("Request cannot be null");
//             }
//
//             if (request.InvoiceData == null)
//             {
//                 return BadRequest("Invoice data cannot be null");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.Template))
//             {
//                 return BadRequest("Template content is required");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.InvoiceData.InvoiceNumber))
//             {
//                 return BadRequest("Invoice number is required");
//             }
//
//             if (request.InvoiceData.Items == null || !request.InvoiceData.Items.Any())
//             {
//                 return BadRequest("At least one invoice item is required");
//             }
//
//             var invoice = MapToInvoice(request.InvoiceData);
//             var html = await _invoiceService.GenerateInvoiceHtmlAsync(invoice, request.InvoiceData.TemplateEngine, request.Template);
//             
//             return Content(html, "text/html");
//         }
//         catch (NotSupportedException ex)
//         {
//             return BadRequest(new { error = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while generating the invoice with custom template", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Generate an invoice HTML using a custom template with the specified template engine and return as downloadable file
//     /// </summary>
//     /// <param name="request">Request containing invoice data, template engine, and custom template</param>
//     /// <returns>HTML file download using the custom template</returns>
//     [HttpPost("generate/custom-template/download")]
//     public async Task<IActionResult> GenerateInvoiceWithCustomTemplateDownload([FromBody] CustomTemplateInvoiceRequest request)
//     {
//         try
//         {
//             if (request == null)
//             {
//                 return BadRequest("Request cannot be null");
//             }
//
//             if (request.InvoiceData == null)
//             {
//                 return BadRequest("Invoice data cannot be null");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.Template))
//             {
//                 return BadRequest("Template content is required");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.InvoiceData.InvoiceNumber))
//             {
//                 return BadRequest("Invoice number is required");
//             }
//
//             if (request.InvoiceData.Items == null || !request.InvoiceData.Items.Any())
//             {
//                 return BadRequest("At least one invoice item is required");
//             }
//
//             var invoice = MapToInvoice(request.InvoiceData);
//             var html = await _invoiceService.GenerateInvoiceHtmlAsync(invoice, request.InvoiceData.TemplateEngine, request.Template);
//             var bytes = System.Text.Encoding.UTF8.GetBytes(html);
//             
//             return File(bytes, "text/html", $"Invoice_{request.InvoiceData.InvoiceNumber}_Custom_{request.InvoiceData.TemplateEngine}.html");
//         }
//         catch (NotSupportedException ex)
//         {
//             return BadRequest(new { error = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while generating the invoice with custom template", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Get available template engines
//     /// </summary>
//     /// <returns>List of supported template engines</returns>
//     [HttpGet("template-engines")]
//     public IActionResult GetSupportedTemplateEngines()
//     {
//         var engines = _invoiceService.GetSupportedEngines();
//         return Ok(new { supportedEngines = engines.Select(e => new { name = e.ToString(), value = (int)e }) });
//     }
//
//     /// <summary>
//     /// Get a sample invoice request for testing purposes (RazorLight)
//     /// </summary>
//     /// <returns>Sample invoice request</returns>
//     [HttpGet("sample")]
//     public IActionResult GetSampleInvoiceRequest()
//     {
//         var sample = new InvoiceRequest
//         {
//             InvoiceNumber = "INV-2024-001",
//             Date = DateTime.Now,
//             DueDate = DateTime.Now.AddDays(30),
//             TemplateEngine = TemplateEngine.RazorLight,
//             BillFrom = new Company
//             {
//                 Name = "Unified Invoice Solutions",
//                 Address = "123 Template Street",
//                 City = "Engine City",
//                 State = "EC",
//                 PostalCode = "12345",
//                 Email = "billing@unifiedinvoice.com",
//                 Phone = "(555) 123-4567"
//             },
//             BillTo = new Company
//             {
//                 Name = "Client Corporation",
//                 Address = "456 Business Ave",
//                 City = "Commerce Town",
//                 State = "CT",
//                 PostalCode = "67890",
//                 Email = "accounts@clientcorp.com",
//                 Phone = "(555) 987-6543"
//             },
//             Items = new List<InvoiceItem>
//             {
//                 new() { Description = "Multi-Engine Invoice System", Quantity = 1, UnitPrice = 2500.00m },
//                 new() { Description = "Template Provider Architecture", Quantity = 1, UnitPrice = 1500.00m },
//                 new() { Description = "Custom Template Support", Quantity = 1, UnitPrice = 1000.00m }
//             },
//             TaxAmount = 500.00m,
//             Notes = "Unified invoice system supporting multiple template engines!"
//         };
//
//         return Ok(sample);
//     }
//
//     /// <summary>
//     /// Get a sample invoice request for testing purposes (Handlebars)
//     /// </summary>
//     /// <returns>Sample invoice request with Handlebars engine</returns>
//     [HttpGet("sample/handlebars")]
//     public IActionResult GetSampleHandlebarsInvoiceRequest()
//     {
//         var sample = new InvoiceRequest
//         {
//             InvoiceNumber = "HB-2024-001",
//             Date = DateTime.Now,
//             DueDate = DateTime.Now.AddDays(30),
//             TemplateEngine = TemplateEngine.Handlebars,
//             BillFrom = new Company
//             {
//                 Name = "Handlebars Solutions Ltd",
//                 Address = "789 Mustache Boulevard",
//                 City = "Template City",
//                 State = "TC",
//                 PostalCode = "54321",
//                 Email = "info@handlebarssolutions.com",
//                 Phone = "(555) 444-5555"
//             },
//             BillTo = new Company
//             {
//                 Name = "Dynamic Web Corp",
//                 Address = "321 Client Street",
//                 City = "Web Town",
//                 State = "WT",
//                 PostalCode = "98765",
//                 Email = "billing@dynamicweb.com",
//                 Phone = "(555) 666-7777"
//             },
//             Items = new List<InvoiceItem>
//             {
//                 new() { Description = "Handlebars Template Development", Quantity = 30, UnitPrice = 95.00m },
//                 new() { Description = "Custom Helper Functions", Quantity = 10, UnitPrice = 150.00m },
//                 new() { Description = "Template Optimization", Quantity = 5, UnitPrice = 200.00m }
//             },
//             TaxAmount = 425.00m,
//             Notes = "Generated using Handlebars templating engine with custom helpers!"
//         };
//
//         return Ok(sample);
//     }
//
//     /// <summary>
//     /// Get a sample custom template request for testing purposes
//     /// </summary>
//     /// <returns>Sample custom template request</returns>
//     [HttpGet("sample/custom-template")]
//     public IActionResult GetSampleCustomTemplateRequest()
//     {
//         var razorTemplate = @"@model InvoiceAPI.Models.Invoice
// <!DOCTYPE html>
// <html>
// <head>
//     <title>Invoice @Model.InvoiceNumber</title>
//     <style>
//         body { font-family: Arial, sans-serif; margin: 20px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
//         .container { background: white; padding: 30px; border-radius: 10px; box-shadow: 0 10px 30px rgba(0,0,0,0.2); }
//         .header { text-align: center; color: #333; margin-bottom: 30px; }
//         .invoice-title { font-size: 32px; font-weight: bold; color: #667eea; }
//         table { width: 100%; border-collapse: collapse; margin: 20px 0; }
//         th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }
//         th { background: #667eea; color: white; }
//         .total { text-align: right; font-size: 18px; font-weight: bold; margin-top: 20px; }
//     </style>
// </head>
// <body>
//     <div class='container'>
//         <div class='header'>
//             <div class='invoice-title'>CUSTOM INVOICE</div>
//             <h2>@Model.InvoiceNumber</h2>
//             <p>Date: @Model.Date.ToString(""yyyy-MM-dd"")</p>
//         </div>
//         <table>
//             <thead><tr><th>Item</th><th>Qty</th><th>Price</th><th>Total</th></tr></thead>
//             <tbody>
//                 @foreach(var item in Model.Items) {
//                     <tr><td>@item.Description</td><td>@item.Quantity</td><td>$@item.UnitPrice.ToString(""F2"")</td><td>$@item.Total.ToString(""F2"")</td></tr>
//                 }
//             </tbody>
//         </table>
//         <div class='total'>Total: $@Model.Total.ToString(""F2"")</div>
//     </div>
// </body>
// </html>";
//
//         var sampleInvoiceData = new InvoiceRequest
//         {
//             InvoiceNumber = "CUSTOM-2024-001",
//             Date = DateTime.Now,
//             DueDate = DateTime.Now.AddDays(30),
//             TemplateEngine = TemplateEngine.RazorLight,
//             BillFrom = new Company
//             {
//                 Name = "Custom Template Co",
//                 Address = "999 Custom Ave",
//                 City = "Template City",
//                 State = "TC",
//                 PostalCode = "99999"
//             },
//             BillTo = new Company
//             {
//                 Name = "Test Client Ltd",
//                 Address = "888 Test Blvd",
//                 City = "Test City",
//                 State = "TC",
//                 PostalCode = "88888"
//             },
//             Items = new List<InvoiceItem>
//             {
//                 new() { Description = "Custom Template Design", Quantity = 1, UnitPrice = 500.00m }
//             },
//             TaxAmount = 50.00m,
//             Notes = "This uses a custom gradient template!"
//         };
//
//         var sample = new CustomTemplateInvoiceRequest
//         {
//             InvoiceData = sampleInvoiceData,
//             Template = razorTemplate
//         };
//
//         return Ok(sample);
//     }
//
//     /// <summary>
//     /// Generate an invoice HTML and create a DocuSeal submission for e-signature
//     /// </summary>
//     /// <param name="request">Request containing invoice data and DocuSeal submission details</param>
//     /// <returns>DocuSeal submission response with signing URLs</returns>
//     [HttpPost("generate/docuseal")]
//     public async Task<IActionResult> GenerateInvoiceAndCreateDocuSealSubmission([FromBody] DocuSealSubmissionRequest request)
//     {
//         try
//         {
//             if (request == null)
//             {
//                 return BadRequest("Request cannot be null");
//             }
//
//             if (request.InvoiceData == null)
//             {
//                 return BadRequest("Invoice data cannot be null");
//             }
//
//             if (string.IsNullOrWhiteSpace(request.InvoiceData.InvoiceNumber))
//             {
//                 return BadRequest("Invoice number is required");
//             }
//
//             if (request.InvoiceData.Items == null || !request.InvoiceData.Items.Any())
//             {
//                 return BadRequest("At least one invoice item is required");
//             }
//
//             if (request.Submitters == null || !request.Submitters.Any())
//             {
//                 return BadRequest("At least one submitter is required");
//             }
//
//             // Validate submitters
//             foreach (var submitter in request.Submitters)
//             {
//                 if (string.IsNullOrWhiteSpace(submitter.Name))
//                 {
//                     return BadRequest("Submitter name is required");
//                 }
//
//                 if (string.IsNullOrWhiteSpace(submitter.Email))
//                 {
//                     return BadRequest("Submitter email is required");
//                 }
//             }
//
//             // Generate invoice HTML
//             var invoice = MapToInvoice(request.InvoiceData);
//             var html = await _invoiceService.GenerateInvoiceHtmlAsync(invoice, request.InvoiceData.TemplateEngine);
//
//             // Create DocuSeal submission with generated HTML
//             var fileName = $"Invoice_{request.InvoiceData.InvoiceNumber}_{request.InvoiceData.TemplateEngine}.html";
//             var subject = request.Subject ?? $"Invoice {request.InvoiceData.InvoiceNumber} - Please Sign";
//             var message = request.Message ?? $"Please review and sign the attached invoice {request.InvoiceData.InvoiceNumber}.";
//
//             var submission = await _docuSealService.CreateSubmissionWithInvoiceHtmlAsync(
//                 html, 
//                 fileName, 
//                 request.Submitters, 
//                 subject, 
//                 message);
//
//             return Ok(new
//             {
//                 success = true,
//                 submission = submission,
//                 invoiceNumber = request.InvoiceData.InvoiceNumber,
//                 templateEngine = request.InvoiceData.TemplateEngine.ToString(),
//                 message = "Invoice generated and DocuSeal submission created successfully"
//             });
//         }
//         catch (HttpRequestException ex)
//         {
//             return StatusCode(502, new { error = "DocuSeal API error", details = ex.Message });
//         }
//         catch (NotSupportedException ex)
//         {
//             return BadRequest(new { error = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while generating invoice and creating DocuSeal submission", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Get DocuSeal templates
//     /// </summary>
//     /// <returns>List of available DocuSeal templates</returns>
//     [HttpGet("docuseal/templates")]
//     public async Task<IActionResult> GetDocuSealTemplates()
//     {
//         try
//         {
//             var templates = await _docuSealService.GetTemplatesAsync();
//             return Ok(templates);
//         }
//         catch (HttpRequestException ex)
//         {
//             return StatusCode(502, new { error = "DocuSeal API error", details = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while fetching DocuSeal templates", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Get DocuSeal submission details
//     /// </summary>
//     /// <param name="submissionId">DocuSeal submission ID</param>
//     /// <returns>DocuSeal submission details</returns>
//     [HttpGet("docuseal/submissions/{submissionId}")]
//     public async Task<IActionResult> GetDocuSealSubmission(int submissionId)
//     {
//         try
//         {
//             var submission = await _docuSealService.GetSubmissionAsync(submissionId);
//             return Ok(submission);
//         }
//         catch (HttpRequestException ex)
//         {
//             return StatusCode(502, new { error = "DocuSeal API error", details = ex.Message });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { error = "An error occurred while fetching DocuSeal submission", details = ex.Message });
//         }
//     }
//
//     /// <summary>
//     /// Get a sample DocuSeal submission request
//     /// </summary>
//     /// <returns>Sample DocuSeal submission request</returns>
//     [HttpGet("sample/docuseal")]
//     public IActionResult GetSampleDocuSealRequest()
//     {
//         var sample = new DocuSealSubmissionRequest
//         {
//             InvoiceData = new InvoiceRequest
//             {
//                 InvoiceNumber = "DS-2024-001",
//                 Date = DateTime.Now,
//                 DueDate = DateTime.Now.AddDays(30),
//                 TemplateEngine = TemplateEngine.RazorLight,
//                 BillFrom = new Company
//                 {
//                     Name = "DocuSeal Invoice Co",
//                     Address = "123 Signature Street",
//                     City = "Document City",
//                     State = "DC",
//                     PostalCode = "12345",
//                     Email = "billing@docusealinvoice.com",
//                     Phone = "(555) 123-4567"
//                 },
//                 BillTo = new Company
//                 {
//                     Name = "Client Signatures Ltd",
//                     Address = "456 Signing Ave",
//                     City = "E-Sign Town",
//                     State = "ES",
//                     PostalCode = "67890",
//                     Email = "payments@clientsignatures.com",
//                     Phone = "(555) 987-6543"
//                 },
//                 Items = new List<InvoiceItem>
//                 {
//                     new() { Description = "E-Signature Services", Quantity = 1, UnitPrice = 1500.00m },
//                     new() { Description = "Document Processing", Quantity = 2, UnitPrice = 750.00m },
//                     new() { Description = "Digital Workflow Setup", Quantity = 1, UnitPrice = 1000.00m }
//                 },
//                 TaxAmount = 325.00m,
//                 Notes = "This invoice will be sent for e-signature via DocuSeal!"
//             },
//             Submitters = new List<DocuSealSubmitter>
//             {
//                 new DocuSealSubmitter
//                 {
//                     Name = "John Doe",
//                     Email = "john.doe@example.com",
//                     Role = "Signer"
//                 },
//                 new DocuSealSubmitter
//                 {
//                     Name = "Jane Smith",
//                     Email = "jane.smith@example.com",
//                     Role = "Signer"
//                 }
//             },
//             SendEmail = true,
//             Subject = "Invoice DS-2024-001 - Please Sign",
//             Message = "Please review and electronically sign this invoice. Thank you!"
//         };
//
//         return Ok(sample);
//     }
//
//     private static Invoice MapToInvoice(InvoiceRequest request)
//     {
//         return new Invoice
//         {
//             InvoiceNumber = request.InvoiceNumber,
//             Date = request.Date,
//             DueDate = request.DueDate,
//             BillTo = request.BillTo,
//             BillFrom = request.BillFrom,
//             Items = request.Items,
//             TaxAmount = request.TaxAmount,
//             Notes = request.Notes,
//             Status = InvoiceStatus.Draft
//         };
//     }
// }