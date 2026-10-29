using System.Web.UI.WebControls;

namespace HostelManagement
{
    public static class GridViewExtensions
    {
        /// <summary>
        /// Renders the header row inside &lt;thead&gt;. A GridView puts it in the table body by default, and the
        /// client-side search/paging (js/datatables-simple-demo.js) only works on tables that have a real header.
        /// Call it after every DataBind().
        /// </summary>
        public static void RenderHeaderInThead(this GridView grid)
        {
            // HeaderRow is null when there is nothing to show (the empty-data message replaces the table).
            if (grid.HeaderRow != null)
                grid.HeaderRow.TableSection = TableRowSection.TableHeader;
        }
    }
}
