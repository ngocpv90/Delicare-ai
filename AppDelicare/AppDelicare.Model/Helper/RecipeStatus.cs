namespace AppDelicare.Model.Helper
{
    /// <summary>Trạng thái phiên bản công thức (STATUS_CATALOG – Recipe) và loại phê duyệt.</summary>
    public static class RecipeStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Locked = "Locked";
        public const string Obsolete = "Obsolete";
        /// <summary>ApprovalRequest.BusinessType của phiên bản công thức.</summary>
        public const string BusinessType = "RECIPE_VERSION";
    }
}
