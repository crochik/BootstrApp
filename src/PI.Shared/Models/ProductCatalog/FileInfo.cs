using System;

namespace PI.ProductCatalog.Models;

public class FileInfo
{
    public Uri Url { get; set; }
    public string Filename { get; set; }
    public DateTime ModifiedDate { get; set; }
}