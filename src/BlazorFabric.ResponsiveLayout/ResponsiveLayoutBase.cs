using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorFabric
{
    public class ResponsiveLayoutBase : ComponentBase
    {
        [Parameter] public RenderFragment ChildContent { get; set; }
      
        public List<ResponsiveLayoutItem> Items { get; set; } = new List<ResponsiveLayoutItem>();
     
        private OrderedDictionary<ResponsiveLayoutItem, string> _mediaQueries = new OrderedDictionary<ResponsiveLayoutItem, string>();
     
        public async Task AddOrUpdateAsync(ResponsiveLayoutItem item)
        {                        
            if (!Items.Contains(item))
            {
                Items.Add(item);
            }

        }

        void GenerateMediaQueries()
        {
            foreach (var item in Items)
            {

            }
        }

       
    }
}
