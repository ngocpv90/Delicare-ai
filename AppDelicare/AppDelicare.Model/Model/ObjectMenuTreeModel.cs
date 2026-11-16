namespace AppDelicare.Model.Model
{
    /// <summary>T45R: một nút của cây menu chức năng (spec 3.4).</summary>
    public class ObjectMenuTreeModel
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? MetaLink { get; set; }
        public Guid? ParentId { get; set; }
        public int? LevelShow { get; set; }
        public int? OrderShow { get; set; }
        public string? IconName { get; set; }
        public bool IsActive { get; set; }
        public List<ObjectMenuTreeModel> Children { get; set; } = [];
    }
}
