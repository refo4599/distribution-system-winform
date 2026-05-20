using System;
using System.Collections.Generic;

namespace DistributionSystem.Business.Dtos
{
    public class InboundOrderDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        /// <summary>ÇáßãíÉ ãÎÒäÉ ÈÇáÚáÈÉ İí DB — ãËÇá: 48 ÚáÈÉ</summary>
        public int Quantity { get; set; }
        /// <summary>
        /// ÚÏÏ ÇáÚáÈ İí ÇáßÑÊæäÉ — íõãáÃ ãä InboundOrderItems.BoxesPerCarton (ÇáŞíãÉ Çááí ÇáãÓÊÎÏã ÃÏÎáåÇ)
        /// </summary>
        public int BoxesPerCarton { get; set; } = 1;
        public decimal PurchasePrice { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "ãßÊãá";
        public int StockQuantity { get; set; }
        public List<InboundOrderItemDto> Items { get; set; } = new List<InboundOrderItemDto>();
    }

    public class InboundOrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }          // ÈÇáÚáÈÉ ÏÇÆãÇğ
        public decimal PurchasePrice { get; set; }
        /// <summary>? ÚÏÏ ÇáÚáÈ İí ÇáßÑÊæäÉ — íÏÎáå ÇáãÓÊÎÏã íÏæíÇğ İí ßá ÃãÑ</summary>
        public int BoxesPerCarton { get; set; } = 1;
    }
}