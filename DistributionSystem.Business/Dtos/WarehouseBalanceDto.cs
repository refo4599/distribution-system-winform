namespace DistributionSystem.Business.Dtos
{
    public class WarehouseBalanceDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// ÇáßãíÉ ÇáÅÌãÇáíÉ ÈÇáÚáÈÉ (ßãÇ åí İí DB)
        /// ãËÇá: 53 ÚáÈÉ
        /// </summary>
        public int Balance { get; set; }

        /// <summary>
        /// ÚÏÏ ÇáÚáÈ İí ÇáßÑÊæäÉ ÇáæÇÍÏÉ áåĞÇ ÇáãäÊÌ
        /// ãËÇá: 24
        /// </summary>
        public int BoxesPerCarton { get; set; } = 1;

        /// <summary>
        /// ÚÏÏ ÇáßÑÇÊíä ÇáßÇãáÉ = Balance / BoxesPerCarton
        /// ãËÇá: 53 / 24 = 2 ßÑÊæäÉ
        /// </summary>
        public int FullCartons => BoxesPerCarton > 0 ? Balance / BoxesPerCarton : 0;

        /// <summary>
        /// ÇáÚáÈ ÇáãÊÈŞíÉ ÈÚÏ ÇáßÑÇÊíä = Balance % BoxesPerCarton
        /// ãËÇá: 53 % 24 = 5 ÚáÈ
        /// </summary>
        public int RemainBoxes => BoxesPerCarton > 0 ? Balance % BoxesPerCarton : Balance;

        /// <summary>
        /// äÕ ÇáÚÑÖ: "2 ßÑÊæäÉ + 5 ÚáÈ" Ãæ "0 ßÑÊæäÉ + 5 ÚáÈ"
        /// </summary>
        public string BalanceDisplay
        {
            get
            {
                if (BoxesPerCarton <= 1) return $"{Balance} ÚáÈÉ";
                if (FullCartons > 0 && RemainBoxes > 0)
                    return $"{FullCartons} ßÑÊæäÉ + {RemainBoxes} ÚáÈÉ";
                if (FullCartons > 0)
                    return $"{FullCartons} ßÑÊæäÉ";
                return $"{RemainBoxes} ÚáÈÉ";
            }
        }

        public decimal AvgCost { get; set; }
        public decimal TotalCost { get; set; }
    }
}