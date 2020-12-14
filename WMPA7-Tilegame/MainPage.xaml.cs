/*
* FILE          : MainWindow.xaml.cs
* PROJECT       : WMPA7-Tilegame (Universal Windows)
* PROGRAMMER    : Balazs Karner 8646201 & Josh Braga 5895818
* FIRST VERSION : 12/13/2020
* DESCRIPTION   :
*       The purpose of this project is to
*/



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



namespace WMPA7_Tilegame
{
    public sealed partial class MainPage : Page
    {
        System.Timers.Timer tmr = new System.Timers.Timer(); //Global timer which elapses every 1 second to text block
        int Counter = 0;                                     //Global int counter to track seconds

        public const int WIN = 0;         //Game state indicates the player has won
        public const int SUSPEND = 1;     //Game state indicates the last shutdown was a suspend
        public const int TERMINATE = 2;   //Game state indicates the last shutdown was a normal suspend and terminate to save game state
        public const int UP = -1;         //Indicates tile must move up
        public const int DOWN = 1;        //Indicates tile must move down
        public const int LEFT = -1;       //Indicates tile must move left
        public const int RIGHT = 1;       //Indicates tile must move right
        public const int ONE_MINUTE = 60; //60 seconds for minute calculations

        // Global translation transform used for changing the position of 
        // the Rectangle based on input data from the touch contact.
        private Dictionary<Rectangle, KeyValuePair<int, int>> RectanglePositions = new Dictionary<Rectangle, KeyValuePair<int, int>>();
        private Rectangle empty = new Rectangle();
        Rectangle[] rectArray;
        List<KeyValuePair<Rectangle, string>> ActiveRectangleDirectionOfMovement = new List<KeyValuePair<Rectangle, string>>();
        Boolean _gameActive = false;
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;


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


            CheckPositions();
            for (int i = 0; i < 500; ++i)
            {
                RandomizeRectangles();
            }

            //Clear the leader board
            leaderboard.Items.Clear();
            string player = null;

            //Add top 10 players to the leader board
            for (int i = 1; i <= 10; i++)
            {
                //Make player string equal to player's name with a colon
                if (localSettings.Values["p" + i.ToString()] != null)
                {
                    player = (string)localSettings.Values["p" + i.ToString()] + ": ";

                    //Add player's score to their name after the colon and add it to the listbox
                    if (localSettings.Values[i.ToString()] != null)
                    {
                        player = player + (string)localSettings.Values[i.ToString()];
                    }
                }

                //Add the player to the leader board if both name and score were not null
                if ((localSettings.Values["p" + i.ToString()] != null) && (localSettings.Values[i.ToString()] != null))
                {
                    leaderboard.Items.Add(player);
                }

                //Reset player string
                player = null;
            }

            //Start the timer for the game
            tmr.Elapsed += Tmr_Elapsed;
            tmr.Interval = 1000;
            tmr.Enabled = true;
            tmr.Start();
            _gameActive = true;


            if (localSettings.Values["gameState"] != null)
            {
                if ((int)localSettings.Values["gameState"] == TERMINATE)
                {
                    DisplaySaveRestoreDialog();
                }
            }
        }

        private async void DisplaySaveRestoreDialog()
        {
            ContentDialog saveRestoreDialog = new ContentDialog
            {
                Title = "Continue Game",
                Content = "Previous save game detected, continue or start new game?",
                PrimaryButtonText = "New Game",
                CloseButtonText = "Continue"                
            };

            tmr.Stop();

            ContentDialogResult result = await saveRestoreDialog.ShowAsync();

            

            if (result == ContentDialogResult.Primary)
            {
                localSettings.Values["gameState"] = WIN;
                _gameActive = false;
                CheckPositions();
                Counter = 0;
                labelCounter.Text = Counter.ToString();
                for (int i = 0; i < 500; ++i)
                {
                    RandomizeRectangles();
                }
            }

            tmr.Start();

        }

        /* 
         * METHOD      : Current_LeavingBackground()
         * DESCRIPTION :
         *      This method 
         * PARAMETERS  :
         *                          object : sender
         *      LeavingBackgroundEventArgs : e
         * RETURNS     :
         *      void : void
         */
        private void Current_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            if (localSettings.Values["gameState"] != null)
            {
                if ((int)localSettings.Values["gameState"] == TERMINATE)
                {
                    RestoreGame();
                }
            }
        }

        /* 
         * METHOD      : Current_Suspending()
         * DESCRIPTION :
         *      This method 
         * PARAMETERS  :
         *                   object : sender
         *      SuspendingEventArgs : e
         * RETURNS     :
         *      void : void
         */
        private void Current_Suspending(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
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

        /* 
         * METHOD      : Current_Resuming()
         * DESCRIPTION :
         *      This method 
         * PARAMETERS  :
         *      object : sender
         *      object : e
         * RETURNS     :
         *      void : void
         */
        public void Current_Resuming(Object sender, object e)
        {
            tmr.Start();

            if (localSettings.Values["count"] != null)
            {
                Object value = localSettings.Values["count"];
                Counter = (int)value;
            }
        }

        /* 
         * METHOD      : RestoreGame()
         * DESCRIPTION :
         *      This method 
         * PARAMETERS  :
         *      void : void
         * RETURNS     :
         *      void : void
         */
        private void RestoreGame()
        {
            //If the count isn't null in local settings then restore count to saved value
            if (localSettings.Values["count"] != null)
            {
                Object value = localSettings.Values["count"];
                Counter = (int)value;
            }

            foreach (Rectangle r in rectArray)
            {
                Object xPosition = null;
                Object yPosition = null;
                Object xPosition2 = null;
                Object yPosition2 = null;

                if (r != empty)
                {
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

            //Check positions of the rectangles
            CheckPositions();
        }

        /* 
         * METHOD      : UpdateTextblock()
         * DESCRIPTION :
         *      This method 
         * PARAMETERS  :
         *      int : newCount
         * RETURNS     :
         *      void : void
         */
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

        /* 
         * METHOD      : Tmr_Elapsed()
         * DESCRIPTION :
         *      This method 
         * PARAMETERS  :
         *                object : sender
         *      ElapsedEventArgs : e
         * RETURNS     :
         *      void : void
         */
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

                    localSettings.Values["gameState"] = WIN;
                    tmr.Stop();

                    //Iterate over top 10 leader board positions
                    for (int i = 1; i <= 10; i++)
                    {
                        //Check if local setting is null
                        if (localSettings.Values[i.ToString()] != null)
                        {
                            //Check if current win is quicker than the i'th rank
                            if (Counter < (int)localSettings.Values[i.ToString()])
                            {
                                //If current win is quicker than i'th rank, make current win the new i'th rank
                                localSettings.Values[i.ToString()] = Counter;

                                //Update the user name for the corresponding ranking as well
                                if (localSettings.Values["userName"] != null)
                                {
                                    localSettings.Values["p" + i.ToString()] = localSettings.Values["userName"];

                                    //Set i to 11, so loop does not reloop, and break
                                    i = 11;
                                    break;
                                }
                            }
                        }
                    }
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