namespace Models;

public class UpdateProcedure : Procedure
{
    public object Query { get; set; }
    public object Update { get; set; }
    public bool Multiple { get; set; }
}