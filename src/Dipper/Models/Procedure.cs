using Crochik.Dipper;

namespace Models;

public class Procedure
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string Description { get; set; }
    public string Collection { get; set; }
    public Parameter[] Parameters { get; set; }
    public string Body { get; set; }
}