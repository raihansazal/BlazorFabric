using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorFabric
{
    public class ResponsiveLayoutItem : ComponentBase
    {
        [CascadingParameter] public ResponsiveLayout CascadingResponsiveLayout { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
        [Parameter] public bool Default { get; set; } = false;

        [Parameter] public CssValue MaxWidth { get; set; }
        [Parameter] public CssValue MinWidth { get; set; }

        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string MediaQuery { get; set; } = "";

        protected override async Task OnParametersSetAsync()
        {
            var tempQuery = GenerateMediaQuery();
            if (tempQuery != MediaQuery)
            {
                MediaQuery = tempQuery;
                await CascadingResponsiveLayout.AddOrUpdateAsync(this);
            }
            await base.OnParametersSetAsync();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", Id);
            builder.AddContent(0, ChildContent);
            builder.CloseElement();
        }
          
        private string GenerateMediaQuery()
        {
            var mediaQuery = "";
            if (MinWidth != null)
                mediaQuery = $"(min-width: {MinWidth.AsLength})";
            if (MaxWidth != null)
            {
                if (string.IsNullOrWhiteSpace(mediaQuery))
                {
                    mediaQuery = $"(min-width: {MinWidth.AsLength})";
                }
                else
                {
                    mediaQuery += $" and (max-width: {MaxWidth.AsLength})";
                }
            }

            return mediaQuery;
        }
    }
}
