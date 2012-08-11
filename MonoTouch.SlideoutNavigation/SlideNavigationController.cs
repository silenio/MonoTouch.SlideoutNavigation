using System;
using MonoTouch.UIKit;
using System.Drawing;

namespace MonoTouch.SlideoutNavigation
{
    public class SlideNavigationController : UIViewController
    {
        private static readonly PointF Origin = new PointF(0, 0);

        private readonly UITapGestureRecognizer _tapGesture;
        private readonly UIPanGestureRecognizer _panGesture;
        private readonly UIViewController _topViewController;
        private UIViewController _menuController;

        //The location of the top view after the slide
        private PointF _topViewAfterXYSlide;

        //The direction the current/last slide
        private SlideDirection _slideDirection;

        //The width of the last slide
        private float _slideWidth;

        /// <summary>
        /// Gets the content controller, also known as the top view.
        /// </summary>
        public UIViewController ContentController { get; private set; }

        /// <summary>
        /// Gets the menu controller if there currently is one visible.
        /// </summary>
        public UIViewController MenuController
        {
            get { return _menuController; }
            private set
            {
                if (_menuController == value)
                    return;

                //If it did once exist, remove it!
                if (_menuController != null)
                {
                    _menuController.RemoveFromParentViewController();
                    _menuController.View.RemoveFromSuperview();
                }

                _menuController = value;

                //We're just freeing this!
                if (_menuController == null)
                    return;

                _menuController.View.Hidden = !IsOpen;

                this.AddChildViewController(_menuController);
                this.View.AddSubview(_menuController.View);

                //Make sure it's always at the back of the view tree
                this.View.SendSubviewToBack(_menuController.View);
            }
        }

        /// <summary>
        /// Gets or sets whether the control is open (already slid out) or closed.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Gets or sets the slide speed.
        /// </summary>
        public float SlideSpeed { get; set; }

        /// <summary>
        /// Gets or sets the slide animation.
        /// </summary>
        public UIViewAnimationOptions SlideAnimation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this
        /// <see cref="MonoTouch.SlideoutNavigation.SlideNavigationController"/> content view has interaction enabled.
        /// </summary>
        public bool ContentViewInteractionEnabled
        {
            get
            {
                var view = _topViewController.View;
                if (view.Subviews.Length > 0)
                    return view.Subviews[0].UserInteractionEnabled;
                return false;
            }
            set
            {
                var view = _topViewController.View;
                if (view.Subviews.Length > 0)
                    view.Subviews[0].UserInteractionEnabled = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoTouch.SlideoutNavigation.SlideNavigationController"/> class.
        /// </summary>
        public SlideNavigationController()
        {
            _topViewController = new UIViewController();
            _topViewController.View.Hidden = true;
            _topViewController.View.AutosizesSubviews = true;
            _topViewController.View.AutoresizingMask = UIViewAutoresizing.All;
            _topViewController.View.ClipsToBounds = false;
            _topViewController.View.BackgroundColor = UIColor.White;
            _topViewController.View.Layer.MasksToBounds = false;

            _panGesture = new UIPanGestureRecognizer();
            _panGesture.MaximumNumberOfTouches = 1;
            _panGesture.MinimumNumberOfTouches = 1;
            _panGesture.AddTarget(() => Pan());

            _tapGesture = new UITapGestureRecognizer();
            _tapGesture.AddTarget(() => Close());
            _tapGesture.NumberOfTapsRequired = 1;

            IsOpen = false;
            SlideSpeed = 1.0f;
            SlideAnimation = UIViewAnimationOptions.CurveEaseInOut;
        }

        /// <Docs>
        /// Called after the controller’s view is loaded into memory.
        /// </Docs>
        /// <summary>
        /// Views the did load.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.AutosizesSubviews = true;
            View.AutoresizingMask = UIViewAutoresizing.All;
            View.ClipsToBounds = true;
            View.Layer.MasksToBounds = true;

            //The top view will be as large as the parent view.
            _topViewController.View.Frame = new RectangleF(0, 0, View.Bounds.Width, View.Bounds.Height);
            this.AddChildViewController(_topViewController);
            this.View.AddSubview(_topViewController.View);
        }

        /// <summary>
        /// Pan the specified view.
        /// </summary>
        /// <param name='view'>
        /// View.
        /// </param>
        private void Pan()
        {
            var view = _topViewController.View;

            if (_panGesture.State == UIGestureRecognizerState.Changed)
            {
                var delta = _panGesture.TranslationInView(view);

                //Depending on the slide direction, zero out the useless direction
                if (_slideDirection == SlideDirection.Left || _slideDirection == SlideDirection.Right)
                    delta.Y = 0f;
                else
                    delta.X = 0f;

                //Make sure the pan does not move us past our origin point in the specific direction of the slide.
                if ((_slideDirection == SlideDirection.Left) && (delta.X < 0))
                    delta.X = 0f;
                else if ((_slideDirection == SlideDirection.Right) && (delta.X > 0))
                    delta.X = 0f;
                else if ((_slideDirection == SlideDirection.Up) && (delta.Y < 0))
                    delta.Y = 0f;
                else if ((_slideDirection == SlideDirection.Down) && (delta.Y > 0))
                    delta.Y = 0f;

                //Modify the frame coordinates
                view.Frame = new RectangleF(new PointF(_topViewAfterXYSlide.X + delta.X, _topViewAfterXYSlide.Y + delta.Y), view.Frame.Size);
            }
            else if (_panGesture.State == UIGestureRecognizerState.Ended || _panGesture.State == UIGestureRecognizerState.Cancelled)
            {
                var velocity = _panGesture.VelocityInView(view);
                var delta = _panGesture.TranslationInView(view);

                //Depending on the slide direction, zero out the useless direction
                if (_slideDirection == SlideDirection.Left || _slideDirection == SlideDirection.Right)
                    velocity.Y = 0f;
                else
                    velocity.X = 0f;

                //Calculate the sum of X + Y, which is really just the contribution of one of them since we
                //zero'ed one of them out. We do this because we can just compare the velocity in a non-directional
                //sense to see if it was fast enough to warent a close.
                var velociySum = velocity.X + velocity.Y;
                if (_slideDirection == SlideDirection.Left || _slideDirection == SlideDirection.Up)
                {
                    if (velociySum > 800f)
                    {
                        Close();
                        return;
                    }
                }
                else
                {
                    if (velociySum < -800f)
                    {
                        Close();
                        return;
                    }
                }

                var halfSlideWidth = _slideWidth / 2f;
                var sumDelta = delta.X + delta.Y;

                //If the change in our position has increased past half of the width it means we're
                //trying to close the slideout naivigation controller.
                if (Math.Abs(sumDelta) > halfSlideWidth)
                {
                    Close();
                    return;
                }

                //If nothing else, animate us back to our normal position!
                var toLocation = CalculateTopFrame(_slideDirection, _slideWidth);
                UIView.Animate(SlideSpeed, 0, SlideAnimation, () => { view.Frame = toLocation; }, () => {}); 
            }
        }

        /// <summary>
        /// Open the specified menuController, direction and slideWidth.
        /// </summary>
        /// <param name='menuController'>The menu that should be displayed</param>
        /// <param name='direction'>The direction to open in. (The top view's slide direction)</param>
        /// <param name='slideWidth'>How far the top view should slide open. (Also the width of the menu controller)</param>
        public void Open(UIViewController menuController, SlideDirection direction, float slideWidth)
        {
            if (menuController == null)
                throw new ArgumentException("menuController cannot be null!");
            if (!IsViewLoaded)
                throw new InvalidOperationException("View must be loaded before switching controllers.");

            //There are two peices of logic here. The first is when the slideout was not visible. This is the typical situation
            //in which case this delegate will be directly called. The second peice of logic occurs when the slideout
            //is already visible and we just want to slideout in a different direction with a potential different controller.
            //In that case, we need to hide, then show again. In both cases, the same show logic gets called. I could have easily
            //just created another function for this, but I wanted to keep the show code in one place.
            Action openAction = () => {

                //We're now visible!
                IsOpen = true;

                //Remember these things just incase.
                _slideDirection = direction;
                _slideWidth = slideWidth;

                //Update the menu controller being used
                MenuController = menuController;

                //Calculate the frames for the menu and the top view
                var topFrame = CalculateTopFrame(direction, slideWidth);
                _topViewAfterXYSlide = topFrame.Location;

                //Set the menu to it's position
                menuController.View.Frame = CalculateMenuFrame(direction, slideWidth);

                //Prevent the user from interacting with the topview's components
                ContentViewInteractionEnabled = false;

                //Allow code to be executed before the show
                OnOpenBegin(direction);

                //Animate the slideout
                UIView.Animate(SlideSpeed, 0, SlideAnimation, () => { 
                    _topViewController.View.Frame = topFrame;
                }, () => { 
                    //Add the tap/pan gesture to close
                    _topViewController.View.AddGestureRecognizer(_tapGesture);
                    _topViewController.View.AddGestureRecognizer(_panGesture);

                    //Allow code to be executed after the show
                    OnOpenComplete();
                }); 
            };

            //If visible, animate the closure, then animate the opening in that direction.
            //There's even a possiblity that it'll open the same direction, but a different menu controller!
            if (IsOpen)
            {
                //Do the hide animation, then immediately do the show animation!
                //Since we were already visible no additional logic needs to be run when we 'hide' since it's just temporary!
                AnimateClose(() => openAction());
            }
            else
            {
                //Show!
                openAction();
            }
        }

        /// <summary>
        ///  Calculates the location offset for the top view based on a direction and how far it should slide.
        ///  If slideWidth is null then the top will assume a position at the edge of the screen.
        /// </summary>
        /// <param name="direction">The direction used to calculate top position</param>
        /// <param name="slideWidth">The width the top should slide. Passing null will result in the top being position off the screen</param>
        private RectangleF CalculateTopFrame(SlideDirection direction, float ?slideWidth)
        {
            //If there is no content, or width, then slide us to the edge of the view.
            if (slideWidth == null || !slideWidth.HasValue || ContentController == null)
            {
                slideWidth = direction.IsHorizontal() ? View.Bounds.Width : View.Bounds.Height;
            }

            var origin = View.Bounds.Location;
            var slideValue = slideWidth.Value;
            switch (direction)
            {
                case SlideDirection.Left:
                    return new RectangleF(new PointF(origin.X - slideValue, origin.Y), _topViewController.View.Frame.Size);
                case SlideDirection.Right:
                    return new RectangleF(new PointF(origin.X + slideValue, origin.Y), _topViewController.View.Frame.Size);
                case SlideDirection.Up:
                    return new RectangleF(new PointF(origin.X, origin.Y - slideValue), _topViewController.View.Frame.Size);
                case SlideDirection.Down:
                    return new RectangleF(new PointF(origin.X, origin.Y + slideValue), _topViewController.View.Frame.Size);
                default:
                    throw new ArgumentException("direction is invalid.");
            }
        }

        /// <summary>
        /// Calculates the frame for the menu (bottom view) based on a direction and how far the top view will slide over.
        /// </summary>
        /// <param name="direction">The direction used to calculate menu position</param>
        /// <param name="slideWidth">The width of the menu. Passing null will result in a menu of full width of parent view</param>
        private RectangleF CalculateMenuFrame(SlideDirection direction, float ?slideWidth)
        {
            var bounds = View.Bounds;

            //If there is no content, or no width, then the menu will take all the space
            if (slideWidth == null || !slideWidth.HasValue || ContentController == null)
                return bounds;

            var slideValue = slideWidth.Value;
            switch (direction)
            {
                case SlideDirection.Left:
                    return new RectangleF(bounds.Width - slideValue, 0, slideValue, bounds.Height);
                case SlideDirection.Right:
                    return new RectangleF(0, 0, slideValue, bounds.Height);
                case SlideDirection.Up:
                    return new RectangleF(0, bounds.Height - slideValue, bounds.Width, bounds.Height);
                case SlideDirection.Down:
                    return new RectangleF(0, 0, bounds.Width, slideValue);
                default:
                    throw new ArgumentException("direction is invalid.");
            }
        }


        /// <summary>
        /// Animates the hide.
        /// </summary>
        private void AnimateClose(Action callback)
        {
            var view = _topViewController.View;

            //Animate the slidein
            UIView.Animate(SlideSpeed, 0, SlideAnimation, () => { 
                view.Frame = new RectangleF(View.Bounds.Location, view.Frame.Size);
            }, () => callback());
        }

        /// <summary>
        /// Hide this instance.
        /// </summary>
        public void Close()
        {
            //Hide only when we're not hidden already
            if (!IsOpen)
                return;

            if (!IsViewLoaded)
                throw new InvalidOperationException("View must be loaded before switching controllers.");

            //We're now hidden!
            IsOpen = false;

            //Allow code to be executed before the hide
            OnCloseBegin();

            AnimateClose(() => {
                ContentViewInteractionEnabled = true;

                //Remove the tap/pan gesture
                _topViewController.View.RemoveGestureRecognizer(_tapGesture);
                _topViewController.View.RemoveGestureRecognizer(_panGesture);

                //Allow the menu controller to be cleaned up
                MenuController = null;

                //Allow code to be executed after the hide
                OnCloseComplete();
            });
        }

        /// <summary>
        /// Selects the content controller to be visible to the user as the top view.
        /// If the navigation controller is open this will hide it.
        /// </summary>
        public void SetContentController(UIViewController view)
        {
            if (!IsViewLoaded)
                throw new InvalidOperationException("View must be loaded before switching controllers.");

            //Allow code to be executed before the select
            OnSetContentControllerBegin();

            if (view != null)
            {
                //Set the content controller
                SetContent(view);

                //Hide when we've made a selection
                Close();
            }
            else
            {
                //Make the menu the full screen.
                //Set the content controller null AFTER only because
                //once we set it to null it will turn the visiblity off.
                if (_menuController != null)
                    SetMenuFull(true, () => SetContent(null));
                else
                    SetContent(null);
            }


            //Allow code to be executed after the select
            OnSetContentControllerComplete();
        }

        public void MakeMenuFull()
        {
            SetMenuFull(true);
        }

        public void RestoreMenu()
        {
            SetMenuFull(false);
        }

        private void SetMenuFull(bool full, Action callback = null)
        {
            if (_menuController == null)
                return;

            float ?width = null;
            if (!full)
                width = _slideWidth;

            //Move the content view offscreen.
            var topFrame = CalculateTopFrame(_slideDirection, width);
            var menuFrame = CalculateMenuFrame(_slideDirection, width);

            //Make the menu the full view
            UIView.Animate(SlideSpeed, 0, SlideAnimation, () => {
                _menuController.View.Frame = menuFrame;
                _topViewController.View.Frame = topFrame;
            }, () => { 
                if (callback != null)
                    callback();
            });
        }

        private void SetContent(UIViewController contentController)
        {
            if (ContentController == contentController)
                return;

            if (ContentController != null)
            {
                ContentController.RemoveFromParentViewController();
                ContentController.View.RemoveFromSuperview();
            }

            ContentController = contentController;

            //Hide the top view if there is no content!
            _topViewController.View.Hidden = (ContentController == null);

            if (ContentController == null)
                return;

            //Set the content controller to fill the parent frame
            var parentFrame = _topViewController.View.Frame;
            ContentController.View.Frame = new RectangleF(Origin, parentFrame.Size);

            this._topViewController.AddChildViewController(ContentController);
            this._topViewController.View.AddSubview(ContentController.View);

            //Bring the content view 
            this._topViewController.View.BringSubviewToFront(ContentController.View);
        }


        /// <summary>
        /// Should autorotate to match orientation? Of course!
        /// </summary>
        public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
        {
            return true;
        }

        /// <summary>
        /// Method called when the show action is complete.
        /// </summary>
        protected virtual void OnOpenComplete()
        {
        }

        /// <summary>
        /// Method called when the show action is about to begin.
        /// </summary>
        protected virtual void OnOpenBegin(SlideDirection direction)
        {
        }

        /// <summary>
        /// Method called when the hide action is complete.
        /// </summary>
        protected virtual void OnCloseComplete()
        {
        }

        /// <summary>
        /// Method called when the hide action is about to begin.
        /// </summary>
        protected virtual void OnCloseBegin()
        {
        }

        /// <summary>
        /// Method called when the user content view selection is complete.
        /// </summary>
        protected virtual void OnSetContentControllerComplete()
        {
        }

        /// <summary>
        /// Method called when the user content view selection is about to begin.
        /// </summary>
        protected virtual void OnSetContentControllerBegin()
        {
        }
    }
}

