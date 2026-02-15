namespace WebApiExample.Infrastructure;

/// <summary>
/// Centralized table schema definitions for single-table design.
/// All entities (Customer, Order, Product) share the AppData table with differentiated key patterns.
/// </summary>
public static class TableDefinitions
{
    /// <summary>
    /// Single table name for all entities in the application.
    /// </summary>
    public const string TableName = "AppData";

    /// <summary>
    /// Hash key attribute name.
    /// </summary>
    public const string PartitionKey = "PK";

    /// <summary>
    /// Range key attribute name.
    /// </summary>
    public const string SortKey = "SK";

    /// <summary>
    /// Key patterns for different entity types in the single-table design.
    /// </summary>
    public static class KeyPatterns
    {
        /// <summary>
        /// Customer key patterns.
        /// </summary>
        public static class Customer
        {
            /// <summary>
            /// Partition key format: CUSTOMER#&lt;id&gt;
            /// </summary>
            public static string PK(string customerId) => $"CUSTOMER#{customerId}";

            /// <summary>
            /// Sort key for customer profile: PROFILE
            /// </summary>
            public const string ProfileSK = "PROFILE";
        }

        /// <summary>
        /// Order key patterns.
        /// </summary>
        public static class Order
        {
            /// <summary>
            /// Partition key format: CUSTOMER#&lt;customerId&gt;
            /// Orders are grouped under their customer's partition.
            /// </summary>
            public static string PK(string customerId) => $"CUSTOMER#{customerId}";

            /// <summary>
            /// Sort key format: ORDER#&lt;orderId&gt;
            /// </summary>
            public static string SK(string orderId) => $"ORDER#{orderId}";

            /// <summary>
            /// Sort key prefix for querying all orders: ORDER#
            /// </summary>
            public const string SKPrefix = "ORDER#";
        }

        /// <summary>
        /// Product key patterns.
        /// </summary>
        public static class Product
        {
            /// <summary>
            /// Partition key format: PRODUCT#&lt;id&gt;
            /// </summary>
            public static string PK(string productId) => $"PRODUCT#{productId}";

            /// <summary>
            /// Sort key for product metadata: METADATA
            /// </summary>
            public const string MetadataSK = "METADATA";
        }
    }
}
