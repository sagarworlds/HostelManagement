namespace HostelManagement
{
    /// <summary>One statistic card on the administrator dashboard.</summary>
    public class StatTile
    {
        public StatTile(string title, int value, string cssClass, string url)
        {
            Title = title;
            Value = value;
            CssClass = cssClass;
            Url = url;
        }

        public string Title { get; private set; }

        public int Value { get; private set; }

        /// <summary>Bootstrap background and text colour classes, e.g. "bg-success text-white".</summary>
        public string CssClass { get; private set; }

        /// <summary>Page with more detail; empty when the tile has no link.</summary>
        public string Url { get; private set; }
    }
}
