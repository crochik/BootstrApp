namespace Controllers
{
    public class BreadcrumbReferenceValue
    {
        public string Id { get; set; }

        public string Value => Count > 0 ? $"{Id} ({Count})" : Id;

        public int Count { get; set; }
    }
}