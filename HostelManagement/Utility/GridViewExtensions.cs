using System.Web.UI.WebControls;

namespace HostelManagement
{
    public static class GridViewExtensions
    {
        /// <summary>
        /// Renders the header row inside &lt;thead&gt;. A GridView puts it in the table body by default; a real
        /// header section is correct table markup and is what the client-side search/paging (js/grid-search.js)
        /// builds its column headings from. Call it after every DataBind().
        /// </summary>
        public static void RenderHeaderInThead(this GridView grid)
        {
            // HeaderRow is null when there is nothing to show (the empty-data message replaces the table).
            if (grid.HeaderRow != null)
                grid.HeaderRow.TableSection = TableRowSection.TableHeader;
        }
    }
}
