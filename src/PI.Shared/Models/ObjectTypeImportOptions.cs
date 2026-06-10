using System;
using PI.Shared.Models.Files;

namespace PI.Shared.Models;

public class ObjectTypeImportOptions
{
    public Guid TempRemoteFolderId { get; set; } = Guid.Parse("343d9c20-a03a-4df9-9ece-28da3f69978d");

    public UploadFileOptions TempUploadFileOptions { get; set; } = new UploadFileOptions
    {
        RemotePath = "{{Context|UserId}}/{{ObjectType}}/{{new UUID}}{{UploadedFile|FileExtension}}",
        Name = "{{new UUID}}{{UploadedFile|FileExtension}}",
    };

    public Guid ImportJobFlowId { get; set; }
    public Guid ImportJobObjectStatusId { get; set; }
}