using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using System.Timers;
using System.Threading;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WMPA7_Tilegame
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        System.Timers.Timer tmr = new System.Timers.Timer();
        int Counter = 0;

        public const int WIN = 0;
        public const int SUSPEND = 1;
        public const int TERMINATE = 2;
        public const int UP = -1;
        public const int DOWN = 1;
        public const int LEFT = -1;
        public const int RIGHT = 1;
        public const int ONE_MINUTE = 60;

        // Global translation transform used for changing the position of 
        // the Rectangle based on input data from the touch contact.
        private Dictionary<Rectangle, KeyValuePair<int, int>> RectanglePositions = new Dictionary<Rectangle, KeyValuePair<int, int>>();
        private Rectangle empty = new Rectangle();
        Rectangle[] rectArray;
        List<KeyValuePair<Rectangle, string>> ActiveRectangleDirectionOfMovement = new List<KeyValuePair<Rectangle, string>>();
        Boolean _gameActive = false;

        public MainPage()
        {
            this.InitializeComponent();
            InitializeComponent();

            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            Application.Current.LeavingBackground += Current_LeavingBackground;

            empty.Name = "empty";

            Rectangle[] temp = {rectangleOne, rectangleTwo, rectangleThree, rectangleFour, rectangleFive,
                                     rectangleSix, rectangleSeven, rectangleEight, rectangleNine, rectangleTen,
                                     rectangleEleven, rectangleTwelve, rectangleThirteen, rectangleFourteen,
                                     rectangleFifteen, empty};
            rectArray = temp;

            int imageNumber = 1;

            //https://stackoverflow.com/questions/41274473/uwp-c-sharp-fill-rectangle-with-image
            foreach (Rectangle r in rectArray)
            {
                string imageUri = "Assets/Tiles/" + imageNumber.ToString() + ".png";

                r.Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(this.BaseUri, imageUri))
                };
                ++imageNumber;
            }

            for (int i = 0; i < 4; ++i)
            {
                for (int k = 0; k < 4; ++k)
                {
                    int index = (i * 4) + k;
                    RectanglePositions.Add(rectArray[index], new KeyValuePair<int, int>(i, k));
                }
            }

            if (localSettings.Values["gameState"] == 2)
            {

            }



            CheckPositions();
            for (int i = 0; i < 500; ++i)
            {
                RandomizeRectangles();
            }

            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            Windows.Storage.ApplicationDataCompositeValue composite = new Windows.Storage.ApplicationDataCompositeValue();


            //Clear the leader board
            leaderboard.Items.Clear();

            //Add top 10 players to the leader board
            for (int i = 10; i > 1; i++)
            {
                //if (localSettings.Values[i.ToString()] != null)
                //{
                //    leaderboard.Items.Add();
                //}
            }

            //Start the timer for the game
            tmr.Elapsed += Tmr_Elapsed;
            tmr.Interval = 1000;
            tmr.Enabled = true;
            tmr.Start();
            _gameActive = true;
        }

        private void Current_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            if (localSettings.Values["gameState"] != null)
            {
                if ((int)localSettings.Values["gameState"] == TERMINATE)
                {
                    RestoreGame();
                }
            }
        }

        private void Current_Suspending(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            //Store counter time and stop it
            localSettings.Values["count"] = Counter;
            tmr.Stop();

            localSettings.Values["gameState"] = SUSPEND;

            foreach (Rectangle r in rectArray)
            {
                if (r != empty)
                {
                    //Get rectangle render transform data
                    TranslateTransform check = (TranslateTransform)r.RenderTransform;
                    double xPosition = check.X;
                    double yPosition = check.Y;

                    //Store render transformations
                    localSettings.Values["Transform." + r.Name + "x"] = xPosition;
                    localSettings.Values["Transform." + r.Name + "y"] = yPosition;
                }

                //Store rectangle positions
                localSettings.Values[r.Name + "x"] = RectanglePositions[r].Key;
                localSettings.Values[r.Name + "y"] = RectanglePositions[r].Value;
            }
        }

        public void Current_Resuming(Object sender, object e)
        {
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            tmr.Start();

            if (localSettings.Values["count"] != null)
            {
                Object value = localSettings.Values["count"];
                Counter = (int)value;
            }
        }

        private void RestoreGame()
        {
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            //If the count isn't null in local settings then restore count to saved value
            if (localSettings.Values["count"] != null)
            {
                Object value = localSettings.Values["count"];
                Counter = (int)value;
            }

            foreach (Rectangle r in rectArray)
            {
                if (r != empty)
                {
                    Object xPosition = null;
                    Object yPosition = null;
                    Object xPosition2 = null;
                    Object yPosition2 = null;

                    //Check if value is null before setting RenderTransform x from local settings
                    if (localSettings.Values["Transform." + r.Name + "x"] != null)
                    {
                        xPosition = localSettings.Values["Transform." + r.Name + "x"];
                    }

                    //Check if value is null before setting RenderTransform y from local settings
                    if (localSettings.Values["Transform." + r.Name + "y"] != null)
                    {
                        yPosition = localSettings.Values["Transform." + r.Name + "y"];
                    }

                    //Check if value is null before setting RenderTransform
                    if ((xPosition != null) && (yPosition != null))
                    {
                        TranslateTransform check = (TranslateTransform)r.RenderTransform;
                        check.X = (double)xPosition;
                        check.Y = (double)yPosition;
                    }

                    //Check if value is null before setting Rectangle position x from local settings
                    if (localSettings.Values[r.Name + "x"] != null)
                    {
                        xPosition2 = localSettings.Values[r.Name + "x"];
                    }

                    //Check if value is null before setting Rectangle position y from local settings
                    if (localSettings.Values[r.Name + "y"] != null)
                    {
                        yPosition2 = localSettings.Values[r.Name + "y"];
                    }

                    //Check if value is null before setting Rectangle position in the dictionary
                    if ((xPosition2 != null) && (yPosition2 != null))
                    {
                        RectanglePositions[r] = new KeyValuePair<int, int>((int)xPosition2, (int)yPosition2);
                    }
                }
            }

            //Check positions of the rectangles
            CheckPositions();
        }

        async public void UpdateTextblock(int newCount)
        {
            var dispatcher = labelCounter.Dispatcher;
            if (!dispatcher.HasThreadAccess)
            {
                await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { UpdateTextblock(Counter); });
            }
            else
            {
                //Get how many seconds have elapsed
                int tempCounter = Counter;
                int seconds = tempCounter % ONE_MINUTE;

                //Get how many minutes have elapsed
                tempCounter = tempCounter - seconds;
                int minutes = tempCounter / ONE_MINUTE;

                labelCounter.Text = minutes + ":" + seconds;
            }
        }

        private void Tmr_Elapsed(object sender, ElapsedEventArgs e)
        {
            Counter += 1;
            UpdateTextblock(Counter);
        }

        private void Rectangle_PointerPressed_MoveUp(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

            Rectangle rect = (Rectangle)sender;

            MoveOnYAxis(rect, UP);

        }

        private void Rectangle_PointerPressed_MoveDown(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Rectangle rect = (Rectangle)sender;

            MoveOnYAxis(rect, DOWN);
        }

        private void Rectangle_PointerPressed_MoveLeft(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

            Rectangle rect = (Rectangle)sender;

            MoveOnXAxis(rect, LEFT);

        }

        private void Rectangle_PointerPressed_MoveRight(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

            Rectangle rect = (Rectangle)sender;

            MoveOnXAxis(rect, RIGHT);
        }


        public void MoveOnXAxis(Rectangle rect, int direction)
        {

            KeyValuePair<int, int> previousEmpty = new KeyValuePair<int, int>(RectanglePositions[empty].Key,
                                                                            RectanglePositions[empty].Value);


            RectanglePositions[empty] = new KeyValuePair<int, int>(RectanglePositions[rect].Key, RectanglePositions[rect].Value);
            RectanglePositions[rect] = new KeyValuePair<int, int>(previousEmpty.Key, previousEmpty.Value);

            TranslateTransform moveRectangle = (TranslateTransform)rect.RenderTransform;

            moveRectangle.X += (200 * direction);

            CheckPositions();

        }


        private void MoveOnYAxis(Rectangle rect, int direction)
        {
            KeyValuePair<int, int> previousEmpty = new KeyValuePair<int, int>(RectanglePositions[empty].Key,
                                                                            RectanglePositions[empty].Value);


            RectanglePositions[empty] = new KeyValuePair<int, int>(RectanglePositions[rect].Key, RectanglePositions[rect].Value);
            RectanglePositions[rect] = new KeyValuePair<int, int>(previousEmpty.Key, previousEmpty.Value);

            TranslateTransform moveRectangle = (TranslateTransform)rect.RenderTransform;

            moveRectangle.Y += (200 * direction);

            CheckPositions();
        }



        private void CheckPositions()
        {
            int emptyRow = RectanglePositions[empty].Key;
            int emptyColumn = RectanglePositions[empty].Value;
            ActiveRectangleDirectionOfMovement.Clear();

            Boolean win = false;

            if (_gameActive == true)
            {
                win = true;
                foreach (Rectangle r in rectArray)
                {
                    if (r != empty)
                    {
                        TranslateTransform check = (TranslateTransform)r.RenderTransform;
                        if (check.X != 0 || check.Y != 0)
                        {
                            win = false;
                            break;
                        }
                    }
                }

                if (win == true)
                {
                    Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                    localSettings.Values["gameState"] = WIN;
                    tmr.Stop();
                }
            }



            foreach (Rectangle r in rectArray)
            {
                r.PointerPressed -= Rectangle_PointerPressed_MoveLeft;
                r.PointerPressed -= Rectangle_PointerPressed_MoveRight;
                r.PointerPressed -= Rectangle_PointerPressed_MoveUp;
                r.PointerPressed -= Rectangle_PointerPressed_MoveDown;
            }


            KeyValuePair<int, int> left = new KeyValuePair<int, int>(emptyRow, emptyColumn - 1);
            KeyValuePair<int, int> right = new KeyValuePair<int, int>(emptyRow, emptyColumn + 1);
            KeyValuePair<int, int> down = new KeyValuePair<int, int>(emptyRow + 1, emptyColumn);
            KeyValuePair<int, int> up = new KeyValuePair<int, int>(emptyRow - 1, emptyColumn);
            

            if (win == false)
            {
                foreach (Rectangle r in rectArray)
                {
                    if (RectanglePositions[r].Equals(left))
                    {
                        r.PointerPressed += Rectangle_PointerPressed_MoveRight;
                        ActiveRectangleDirectionOfMovement.Add(new KeyValuePair<Rectangle, string>(r, "RIGHT"));
                    }
                    else if (RectanglePositions[r].Equals(right))
                    {
                        r.PointerPressed += Rectangle_PointerPressed_MoveLeft;
                        ActiveRectangleDirectionOfMovement.Add(new KeyValuePair<Rectangle, string>(r, "LEFT"));
                    }
                    else if (RectanglePositions[r].Equals(down))
                    {
                        r.PointerPressed += Rectangle_PointerPressed_MoveUp;
                        ActiveRectangleDirectionOfMovement.Add(new KeyValuePair<Rectangle, string>(r, "UP"));
                    }
                    else if (RectanglePositions[r].Equals(up))
                    {
                        r.PointerPressed += Rectangle_PointerPressed_MoveDown;
                        ActiveRectangleDirectionOfMovement.Add(new KeyValuePair<Rectangle, string>(r, "DOWN"));
                    }
                }
            }
            else
            {
                winMessageBox.Text = "YOU WIN!!!";
            }

        }

        void RandomizeRectangles()
        {
            Random r = new Random(DateTime.Now.Millisecond);
            int selection = r.Next(0, ActiveRectangleDirectionOfMovement.Count);
            Rectangle rectSelect = ActiveRectangleDirectionOfMovement[selection].Key;


            if (ActiveRectangleDirectionOfMovement[selection].Value == "RIGHT")
            {
                MoveOnXAxis(ActiveRectangleDirectionOfMovement[selection].Key, RIGHT);
            }
            else if (ActiveRectangleDirectionOfMovement[selection].Value == "LEFT")
            {
                MoveOnXAxis(ActiveRectangleDirectionOfMovement[selection].Key, LEFT);
            }
            else if (ActiveRectangleDirectionOfMovement[selection].Value == "UP")
            {
                MoveOnYAxis(ActiveRectangleDirectionOfMovement[selection].Key, UP);
            }
            else if (ActiveRectangleDirectionOfMovement[selection].Value == "DOWN")
            {
                MoveOnYAxis(ActiveRectangleDirectionOfMovement[selection].Key, DOWN);
            }
        }
    }
}