// using System;
// using System.Threading.Tasks;
// using GenerativeAI;
// using GenerativeAI.Types;
// using IdentityModel;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Exceptions;
// using Services;
//
// namespace PI.LangChain.Controllers;
//
// [Microsoft.AspNetCore.Components.Route("/langchain/v1/[controller]")]
// public class GeminiController : APIController
// {
//     [Authorize("admin")]
//     [HttpPost("Test")]
//     public async Task<object> ReplaceFloorTestAsync([FromServices] GeminiAssistantProvider provider)
//     {
//         var client = await provider.GetClientAsync(Context);
//         if (!client.IsSuccess) throw new BadRequestException(client.Status);
//         var model = client.Value.CreateGeminiModel("gemini-2.5-flash-image-preview");
//
//         var systemPrompt = "Using the two images provided, generate a new image that realistically shows the flooring material from the second image applied to the room in the first image. try to transfer the texture/surface/reflection attributes of the floor in the second image when copying it to the first.";
//         var request = new GenerateContentRequest
//         {
//             SystemInstruction = new Content(systemPrompt, Roles.System),
//         };
//         request.AddInlineFile(@"/Users/felipe/temp/floor1.jpg", false);
//         request.AddInlineFile(@"/Users/felipe/temp/floor2.jpg", false);
//
//         var chat = model.StartChat(systemInstruction: systemPrompt);
//         var streamResponse = chat.StreamContentAsync(request.Contents);
//
//         // var streamResponse = model.StreamContentAsync(request);
//         var output = "";
//         var start = DateTime.Now;
//         await foreach (var chunk in streamResponse)
//         {
//             if (chunk.Candidates?.Length > 0)
//             {
//                 var count = 0;
//                 foreach (var candidate in chunk.Candidates)
//                 {
//                     if (candidate.Content?.Parts == null || candidate.Content.Parts.Count == 0)
//                     {
//                         Console.WriteLine($"{(DateTime.Now - start).TotalMilliseconds}: No parts");
//                         continue;
//                     }
//
//                     foreach (var part in candidate.Content.Parts)
//                     {
//                         if (part.Text != null)
//                         {
//                             output += chunk;
//                             Console.WriteLine($"{(DateTime.Now - start).TotalMilliseconds}: Text, {chunk}");
//                             continue;
//                         }
//
//                         if (part.InlineData?.Data != null)
//                         {
//                             var imageData = Convert.FromBase64String(part.InlineData.Data);
//                             var path = $"/Users/felipe/temp/{Guid.NewGuid()}_{++count}";
//                             await System.IO.File.WriteAllBytesAsync(path, imageData);
//                             Console.WriteLine($"{(DateTime.Now - start).TotalMilliseconds}: Created {part.InlineData.MimeType}: {path}");
//                         }
//                     }
//                 }
//             }
//             else
//             {
//                 Console.WriteLine($"{(DateTime.Now - start).TotalMilliseconds}: no candidates?");
//             }
//         }
//
//         return output;
//
//         // var response = await model.GenerateContentAsync(request);
//         //
//         // var count = 0;
//         // foreach (var candidate in response.Candidates)
//         // {
//         //     if (candidate.Content == null) continue;
//         //     foreach (var part in candidate.Content.Parts)
//         //     {
//         //         if (part.InlineData?.Data != null)
//         //         {
//         //             var imageData = Convert.FromBase64String(part.InlineData.Data);
//         //             await System.IO.File.WriteAllBytesAsync($"/Users/felipe/temp/{Guid.NewGuid()}_{++count}", imageData);
//         //         }
//         //     }
//         // }
//         //
//         // return response.Text;
//     }
// }