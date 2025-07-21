using System;

namespace player.contexts
{
    public class SearchContext : GeneralFiniteContext
    {
        public String SearchTerm;

        public SearchContext(String context, String searchTerm) : base(context)
        {
            SearchTerm = searchTerm;
        }
    }
}