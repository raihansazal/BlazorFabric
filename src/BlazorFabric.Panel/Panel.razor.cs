﻿using BlazorFabric.PanelInternal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BlazorFabric
{
    public partial class Panel : FabricComponentBase, IDisposable
    {
        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string CloseButtonAriaLabel { get; set; }

        [Parameter]
        public string CustomWidth { get; set; }

        [Parameter]
        public ElementReference ElementToFocusOnDismiss { get; set; }

        [Parameter]
        public string FirstFocusableSelector { get; set; }

        [Parameter]
        public RenderFragment FooterTemplate { get; set; }

        [Parameter]
        public bool ForceFocusInsideTrap{ get; set; }

        [Parameter]
        public bool HasCloseButton { get; set; } = true;

        [Parameter]
        public string HeaderClassName { get; set; }

        [Parameter]
        public RenderFragment HeaderTemplate { get; set; }

        [Parameter]
        public string HeaderText { get; set; }

        [Parameter]
        public bool IsBlocking { get; set; } = true;

        [Parameter]
        public bool IsFooterAtBottom { get; set; } = false;

        [Parameter]
        public bool IsHiddenOnDismiss { get; set; } = false;

        [Parameter]
        public bool IsLightDismiss { get; set; } = false;

        [Parameter]
        public bool IsOpen { get; set; }

        [Parameter]
        public RenderFragment NavigationTemplate { get; set; }

        [Parameter]
        public RenderFragment NavigationContentTemplate { get; set; }

        [Parameter]
        public EventCallback OnDismiss { get; set; }
   
        [Parameter]
        public EventCallback OnDismissed { get; set; }

        [Parameter]
        public EventCallback OnLightDismissClick { get; set; }

        [Parameter]
        public EventCallback OnOpen { get; set; }

        [Parameter]
        public EventCallback OnOpened { get; set; }

        [Parameter]
        public EventCallback OnOuterClick { get; set; }

        [Parameter]
        public PanelType Type { get; set; } = PanelType.SmallFixedFar;

        private bool isAnimating = false;
        private bool animationRenderStart = false;

        private EventCallback ThrowawayCallback;

        private PanelVisibilityState previousVisibility = PanelVisibilityState.Closed;
        private PanelVisibilityState currentVisibility = PanelVisibilityState.Closed;
        private bool isFooterSticky = false;

        private Action onPanelClick;
        private Action _dismiss;
        private List<int> _scrollerEventId = new List<int>();
        private int _resizeId = -1;
        private int _mouseDownId = -1;

        private Timer _animationTimer;
        private Action _clearExistingAnimationTimer;
        private Action<PanelVisibilityState> _animateTo;
        private Action _onTransitionComplete;

        private ElementReference panelElement;
        private ElementReference scrollableContent;
        private bool _scrollerRegistered;

        private ElapsedEventHandler _handler = null;

        private bool _jsAvailable = false;

        public Panel()
        {
            Debug.WriteLine("Panel Created");
            _animationTimer = new Timer();

            HeaderTemplate = builder =>
            {
                if (HeaderText != null)
                {
                    builder.OpenElement(0, "div");
                    {
                        builder.AddAttribute(1, "class", "ms-Panel-header");
                        builder.OpenElement(2, "p");
                        {
                            builder.AddAttribute(3, "class", "xlargeFont ms-Panel-headerText");
                            //builder.AddAttribute(4, "id", )
                            builder.AddAttribute(5, "role", "heading");
                            builder.AddAttribute(6, "aria-level", "2");
                            builder.AddContent(7, HeaderText);
                        }
                        builder.CloseElement();
                    }
                    builder.CloseElement();
                }
            };

            onPanelClick = () =>
            {
                this._dismiss();
            };
                        
            _dismiss = async () =>
            {
                await OnDismiss.InvokeAsync(null);
                //normally, would check react synth events to see if event was interrupted from the OnDismiss callback before calling the following... 
                // To Do
                this.Close();
            };

            _clearExistingAnimationTimer = () =>
            {
                if (_animationTimer.Enabled)
                {
                    _animationTimer.Stop();
                    _animationTimer.Elapsed -= _handler;
                }
            };

            _animateTo = (animationState) =>
            {
                _animationTimer.Interval = 200;
                _handler = null;
                _handler = (s, e) =>
                {
                    InvokeAsync(() =>
                    {
                        //Debug.WriteLine("Inside invokeAsync from animateTo timer elapsed");
                        _animationTimer.Elapsed -= _handler;
                        _animationTimer.Stop();
                        
                        previousVisibility = currentVisibility;
                        currentVisibility = animationState;
                        _onTransitionComplete();
                    });
                };
                _animationTimer.Elapsed += _handler;
                _animationTimer.Start();
            };

            _onTransitionComplete = async () =>
            {
                isAnimating = false;
                //StateHasChanged();
                await UpdateFooterPositionAsync();

                if (currentVisibility == PanelVisibilityState.Open)
                {
                    await OnOpened.InvokeAsync(null);
                }
                if (currentVisibility == PanelVisibilityState.Closed)
                {
                    await OnDismissed.InvokeAsync(null);
                }
                StateHasChanged();
            };

        }

        //Task OnPanelClick()
        //{
        //    this.dismiss();
        //    return Task.CompletedTask;
        //}

        [JSInvokable]
        public async Task UpdateFooterPositionAsync()
        {
            //Debug.WriteLine("Calling UpdateFooterPositionAsync");
            var clientHeight = await JSRuntime.InvokeAsync<double>("BlazorFabricBaseComponent.getClientHeight", scrollableContent);
            var scrollHeight = await JSRuntime.InvokeAsync<double>("BlazorFabricBaseComponent.getScrollHeight", scrollableContent);

            if (clientHeight < scrollHeight)
                isFooterSticky = true;
            else
                isFooterSticky = false;
        }

        [JSInvokable]
        public async Task DismissOnOuterClick(bool contains)
        {
            if (IsActive())
            {
                //Debug.WriteLine("Calling DismissOnOuterClick");
                //var contains = await JSRuntime.InvokeAsync<bool>("BlazorFabricFocusTrapZone.elementContains", panelElement, targetElement);
                if (!contains)
                {
                    //Debug.WriteLine("Contains is false!");
                    if (OnOuterClick.HasDelegate)
                    {
                        await OnOuterClick.InvokeAsync(null);
                        //need to prevent default for bubbling maybe.  Test with lightdismiss ...
                    }
                    else
                    {
                        _dismiss();
                    }
                }
            }
        }

        public override Task SetParametersAsync(ParameterView parameters)
        {

            return base.SetParametersAsync(parameters);
        }

        public void Open()
        {
            //ignore these calls if we have isOpen set... isOpen need to be nullable in this case... 
            // To Do

        }

        public void Close()
        {

            //ignore these calls if we have isOpen set... isOpen need to be nullable in this case... 
            // To Do
        }

        private string GetTypeCss()
        {
            switch (Type)
            {
                case PanelType.SmallFixedNear:
                    return " ms-Panel--smLeft";
                case PanelType.SmallFixedFar:
                    return " ms-Panel--sm";
                case PanelType.SmallFluid:
                    return " ms-Panel--smFluid";
                case PanelType.Medium:
                    return " ms-Panel--md";
                case PanelType.Large:
                    return " ms-Panel--lg";
                case PanelType.LargeFixed:
                    return " ms-Panel--fixed";
                case PanelType.ExtraLarge:
                    return " ms-Panel--xl";
                case PanelType.Custom:
                    return " ms-Panel--custom";
                case PanelType.CustomNear:
                    return " ms-Panel--customLeft";
                default:
                    return "";
            }
        }

        protected string GetMainAnimation()
        {
            bool isOnRightSide = true;
            switch (Type)
            { 
                // this changes in RTL env, To Do
                case PanelType.SmallFixedNear:
                case PanelType.CustomNear:
                    isOnRightSide = false;
                    break;
            }
            if (IsOpen && isAnimating && !isOnRightSide)
                return " slideRightIn40";
            if (IsOpen && isAnimating && isOnRightSide)
                return " slideLeftIn40";
            if (!IsOpen && isAnimating && !isOnRightSide)
                return " slideLeftOut40";
            if (!IsOpen && isAnimating && isOnRightSide)
                return " slideRightOut40";
            return "";
        }

        private async Task SetRegistrationsAsync()
        {
            //if (ShouldListenForOuterClick())
            //{
            //    _mouseDownId = await JSRuntime.InvokeAsync<int>("BlazorFabricPanel.registerMouseDownHandler", panelElement, DotNetObjectReference.Create(this));
            //}
            
            if (ShouldListenForOuterClick() && _mouseDownId == -1)
            {
                _mouseDownId = -2;
                _mouseDownId = await JSRuntime.InvokeAsync<int>("BlazorFabricPanel.registerMouseDownHandler", panelElement, DotNetObjectReference.Create(this));
            }
            else if (!ShouldListenForOuterClick() && _mouseDownId > -1)
            {
                await JSRuntime.InvokeVoidAsync("BlazorFabricPanel.unregisterHandler", _mouseDownId);
            }

            if (IsOpen && _resizeId == -1)
            {
                _resizeId = -2;
                _resizeId = await JSRuntime.InvokeAsync<int>("BlazorFabricPanel.registerSizeHandler", DotNetObjectReference.Create(this));
                //listen for lightdismiss
            }
            else if (!IsOpen && _resizeId > -1)
            {
                await JSRuntime.InvokeVoidAsync("BlazorFabricPanel.unregisterHandler", _resizeId);
            }

            if (IsOpen && !_scrollerRegistered)
            {
                _scrollerRegistered = true;
                _scrollerEventId = await JSRuntime.InvokeAsync<List<int>>("BlazorFabricPanel.makeElementScrollAllower", scrollableContent);
            }

            if (!IsOpen && _scrollerRegistered)
            {
                _scrollerRegistered = false;
                //var tempArray = _scrollerEventId.ToArray();
                //_scrollerEventId.Clear();
                foreach (var id in _scrollerEventId)
                {
                    await JSRuntime.InvokeVoidAsync("BlazorFabricPanel.unregisterHandler", id);
                }
                _scrollerEventId.Clear();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _jsAvailable = true;
            }
            await SetRegistrationsAsync();

            await base.OnAfterRenderAsync(firstRender);
        }

        protected override async Task OnParametersSetAsync()
        {
            previousVisibility = currentVisibility;

            if (!OnLightDismissClick.HasDelegate)
            {
                OnLightDismissClick = EventCallback.Factory.Create(this, onPanelClick);
            }

            if (IsOpen && (currentVisibility == PanelVisibilityState.Closed || currentVisibility == PanelVisibilityState.AnimatingClosed))
            {
                currentVisibility = PanelVisibilityState.AnimatingOpen;
            }
            if (!IsOpen && (currentVisibility == PanelVisibilityState.Open || currentVisibility == PanelVisibilityState.AnimatingOpen))
            {
                currentVisibility = PanelVisibilityState.AnimatingClosed;
                // This StateHasChanged call was added because using a custom close button in NavigationTemplate did not cause a state change to occur.
                // The result was that the animation class would not get added and the close transition would not show.  This is a hack to make it work.
                StateHasChanged();
            }

            Debug.WriteLine($"Was: {previousVisibility}  Current:{currentVisibility}");

            if (_jsAvailable)
            {
                if (currentVisibility != previousVisibility)
                {
                    Debug.WriteLine("Clearing animation timer");
                    _clearExistingAnimationTimer();
                    if (currentVisibility == PanelVisibilityState.AnimatingOpen)
                    {
                        isAnimating = true;
                        animationRenderStart = true;
                        _animateTo(PanelVisibilityState.Open);
                    }
                    else if (currentVisibility == PanelVisibilityState.AnimatingClosed)
                    {
                        isAnimating = true;
                        //animationRenderStart = true;
                        _animateTo(PanelVisibilityState.Closed);
                    }
                }

                //await SetRegistrationsAsync();
            }
            

            await base.OnParametersSetAsync();
        }

        protected override bool ShouldRender()
        {
            if (isAnimating && !animationRenderStart)
                return false;
            else
            {
                animationRenderStart = false;
                return true;
            }
            //return base.ShouldRender();
        }

        private bool IsActive()
        {
            return currentVisibility == PanelVisibilityState.Open || currentVisibility == PanelVisibilityState.AnimatingOpen;
        }

        private bool ShouldListenForOuterClick()
        {
            return IsBlocking && IsOpen;
        }

        public async void Dispose()
        {
            _clearExistingAnimationTimer();
            if (_scrollerEventId != null)
            {
                foreach (var id in _scrollerEventId)
                {                    
                    await JSRuntime.InvokeVoidAsync("BlazorFabricPanel.unregisterHandler", id);
                }
                _scrollerEventId.Clear();
            }

            if (_resizeId != -1)
            {
                await JSRuntime.InvokeVoidAsync("BlazorFabricPanel.unregisterHandler", _resizeId);
            }
            if (_mouseDownId != -1)
            {
                await JSRuntime.InvokeVoidAsync("BlazorFabricPanel.unregisterHandler", _mouseDownId);
            }
        }
    }
}
