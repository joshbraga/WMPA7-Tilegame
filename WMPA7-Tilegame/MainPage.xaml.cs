/*
* FILE          : MainWindow.xaml.cs
* PROJECT       : WMPA7-Tilegame (Universal Windows)
* PROGRAMMER    : Balazs Karner 8646201 & Josh Braga 5895818
* FIRST VERSION : 12/13/2020
* DESCRIPTION   :
*       The purpose of this project is to creata simple 4x4 tile game with a leaderboard
*       in UWP.
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

        public const int WIN = 0;                   //Game state indicates the player has won
        public const int SUSPEND = 1;               //Game state indicates the last shutdown was a suspend
        public const int TERMINATE = 2;             //Game state indicates the last shutdown was a normal suspend and terminate to save game state
        public const int UP = -1;                   //Indicates tile must move up
        public const int DOWN = 1;                  //Indicates tile must move down
        public const int LEFT = -1;                 //Indicates tile must move left
        public const int RIGHT = 1;                 //Indicates tile must move right
        public const int ONE_MINUTE = 60;           //60 seconds for minute calculations
        public const int RANDOMIZE_COUNT = 1;       //Iterate randomize 500 times
        public const int TRANSLATE_DISTANCE = 200;  //Distance squares move

        // Global translation transform used for changing the position of 
        // the Rectangle based on input data from the touch contact.
        private Dictionary<Rectangle, KeyValuePair<int, int>> RectanglePositions = new Dictionary<Rectangle, KeyValuePair<int, int>>();
        private Rectangle empty = new Rectangle();
        Rectangle[] rectArray;
        List<KeyValuePair<Rectangle, string>> ActiveRectangleDirectionOfMovement = new List<KeyValuePair<Rectangle, string>>();
        Boolean _gameActive = false;
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;




        // METHOD           :   MainPage
        // DESCRIPTION      :   Constructor to the MainPage, sets up event handlers, populates
        //                      initial data
        //
        // PARAMETERS       :
        //
        // RETURNS          :
        //
        public MainPage()
        {
            this.InitializeComponent();
            InitializeComponent();

            //set event listeners
            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            Application.Current.LeavingBackground += Current_LeavingBackground;

            empty.Name = "empty";

            //initialize the indexing array
            Rectangle[] temp = {rectangleOne, rectangleTwo, rectangleThree, rectangleFour, rectangleFive,
                                     rectangleSix, rectangleSeven, rectangleEight, rectangleNine, rectangleTen,
                                     rectangleEleven, rectangleTwelve, rectangleThirteen, rectangleFourteen,
                                     rectangleFifteen, empty};
            rectArray = temp;

            //setting the tiles to images in the assets
            int imageNumber = 1;

            //REFERNCE:
            //Moon, D. (2016, Dec, 22). UWP C# Fill Rectangle with Image
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

            //setting the virtual grid for game grid positioning
            for (int i = 0; i < 4; ++i)
            {
                for (int k = 0; k < 4; ++k)
                {
                    int index = (i * 4) + k;
                    RectanglePositions.Add(rectArray[index], new KeyValuePair<int, int>(i, k));
                }
            }

            //Populate the leader board with placeholders
            for (int i = 1; i < 11; i++)
            {
                if (localSettings.Values[i.ToString()] == null)
                {
                    localSettings.Values[i.ToString()] = Int32.MaxValue;
                }

                if (localSettings.Values["p" + i.ToString()] == null)
                {
                    localSettings.Values["p" + i.ToString()] = ("No User " + i.ToString());
                }
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
                        int playerScore = (int)localSettings.Values[i.ToString()];
                        string dummyScore = "";

                        //If this leader board rank is still a placeholder, display a blank score instead of int32.MaxValue
                        if (playerScore == Int32.MaxValue)
                        {
                            dummyScore = " ";
                        }
                        //Otherwise display the correct player's score
                        else
                        {
                            dummyScore = playerScore.ToString();
                        }

                        player = player + dummyScore;
                    }
                }

                //Add the player to the leader board if both name and score were not null
                if ((localSettings.Values["p" + i.ToString()] != null) && (localSettings.Values[i.ToString()] != null))
                {
                    leaderboard.Items.Add(i + ". " + player);
                }

                //Reset player string
                player = null;
            }

            //Prepare the timer for the game
            tmr.Elapsed += Tmr_Elapsed;
            tmr.Interval = 1000;
            tmr.Enabled = true;
            tmr.Stop();

            //allow user input for username
            usernameInput.IsEnabled = true;

            //displays the continue/newgame dialog if restored from a suspended to terminated state
            if (localSettings.Values["gameState"] != null)
            {
                if ((int)localSettings.Values["gameState"] == TERMINATE)
                {
                    DisplaySaveRestoreDialog();
                }
            }
        }



        // METHOD           :   DisplaySaveRestoreDialog
        // DESCRIPTION      :   Opens a dialog to ask the user if they want to continue
        //                      from previous save or start a new game.
        //
        // PARAMETERS       :
        //  Nothing.
        // RETURNS          :
        //  Nothing.
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

            
            //new game selection
            if (result == ContentDialogResult.Primary)
            {
                localSettings.Values["gameState"] = WIN;                

                //disable all tiles
                foreach (Rectangle r in rectArray)
                {
                    r.PointerPressed -= Rectangle_PointerPressed_MoveLeft;
                    r.PointerPressed -= Rectangle_PointerPressed_MoveRight;
                    r.PointerPressed -= Rectangle_PointerPressed_MoveUp;
                    r.PointerPressed -= Rectangle_PointerPressed_MoveDown;
                }
                //allow username input
                usernameInput.IsEnabled = true;

            }
            //continue from old save, enable timer
            else
            {
                tmr.Start();
            }

            

        }

        /* 
         * METHOD      : Current_LeavingBackground()
         * DESCRIPTION :
         *      This method takes the event fired when the application leaves the background state and uses it
         *      to restore data if it was launched from a terminated state.
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
                //If game was loaded from terminate and suspend normally then prompt user to restart or continue and restore game
                if ((int)localSettings.Values["gameState"] == TERMINATE)
                {
                    RestoreGame();
                }
            }
        }

        /* 
         * METHOD      : Current_Suspending()
         * DESCRIPTION :
         *      This method triggers on suspend and saves all the data needed to restore the state of the
         *      game in the event of a suspend or suspend and terminate.
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
         *      This method triggers on resuming of the game and will restore the state of the timer
         *      and restart it, but restore nothing else as they are already preserved.
         * PARAMETERS  :
         *      object : sender
         *      object : e
         * RETURNS     :
         *      void : void
         */
        public void Current_Resuming(Object sender, object e)
        {
            tmr.Start();

            //If count is not null in localsettings, reset timer to suspended time
            if (localSettings.Values["count"] != null)
            {
                Object value = localSettings.Values["count"];
                Counter = (int)value;
            }
        }

        /* 
         * METHOD      : RestoreGame()
         * DESCRIPTION :
         *      This method is called by the event for leaving suspend to restore the game state.
         *      Restores all values from the localSettings into the game.
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

            if (localSettings.Values["userName"] != null)
            {
                usernameInput.Text = localSettings.Values["userName"].ToString();
                usernameInput.IsEnabled = false;
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
         *      This method allow the timer thread to update the timer counter without UI thread access.
         * PARAMETERS  :
         *      int : newCount :    contains new value to write to timer
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
         *      This method is an event that triggers when the timer fires its event, increments the
         *      counter by one and updates the counter value on screen.
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



        // METHOD           :   Rectangle_PointerPressed_MoveUp
        // DESCRIPTION      :   This event is fired when a tile is clicked and is specifically for moving
        //                      tiles upward.
        //
        // PARAMETERS       :
        //  object sender               :   self reference calling event
        //  PointerRoutedEventArgs e    :   event fired on click
        //
        // RETURNS          :
        //  Nothing.
        //
        private void Rectangle_PointerPressed_MoveUp(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //gets object reference to tile from fired event and sends it to the appropriate movement function
            Rectangle rect = (Rectangle)sender;
            MoveOnYAxis(rect, UP);

        }




        // METHOD           :   Rectangle_PointerPressed_MoveDown
        // DESCRIPTION      :   This event is fired when a tile is clicked and is specifically for moving
        //                      tiles downward.
        //
        // PARAMETERS       :
        //  object sender               :   self reference calling event
        //  PointerRoutedEventArgs e    :   event fired on click
        //
        // RETURNS          :
        //  Nothing.
        //
        private void Rectangle_PointerPressed_MoveDown(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //gets object reference to tile from fired event and sends it to the appropriate movement function
            Rectangle rect = (Rectangle)sender;
            MoveOnYAxis(rect, DOWN);
        }




        // METHOD           :   Rectangle_PointerPressed_MoveLeft
        // DESCRIPTION      :   This event is fired when a tile is clicked and is specifically for moving
        //                      tiles to the left.
        //
        // PARAMETERS       :
        //  object sender               :   self reference calling event
        //  PointerRoutedEventArgs e    :   event fired on click
        //
        // RETURNS          :
        //  Nothing.
        //
        private void Rectangle_PointerPressed_MoveLeft(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //gets object reference to tile from fired event and sends it to the appropriate movement function
            Rectangle rect = (Rectangle)sender;
            MoveOnXAxis(rect, LEFT);

        }




        // METHOD           :   Rectangle_PointerPressed_MoveRight
        // DESCRIPTION      :   This event is fired when a tile is clicked and is specifically for moving
        //                      tiles to the right.
        //
        // PARAMETERS       :
        //  object sender               :   self reference calling event
        //  PointerRoutedEventArgs e    :   event fired on click
        //
        // RETURNS          :
        //  Nothing.
        //
        private void Rectangle_PointerPressed_MoveRight(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //gets object reference to tile from fired event and sends it to the appropriate movement function
            Rectangle rect = (Rectangle)sender;
            MoveOnXAxis(rect, RIGHT);
        }




        // METHOD           :   MoveOnXAxis
        // DESCRIPTION      :   Calculates the movement necessary for the tile to move on the X axis.
        //                      can go left or right depending on parameter passed in
        //
        // PARAMETERS       :
        //  Rectangle rect  :   contains the object ref to rectangle to be moved
        //  int direction   :   contains direction to move, -1 is left +1 is right
        //
        // RETURNS          :
        //  Nothing.
        //
        public void MoveOnXAxis(Rectangle rect, int direction)
        {
            //keeping old empty position
            KeyValuePair<int, int> previousEmpty = new KeyValuePair<int, int>(RectanglePositions[empty].Key,
                                                                            RectanglePositions[empty].Value);

            //swapping the positions of the empty and tile to move in the virtual grid within the collection
            RectanglePositions[empty] = new KeyValuePair<int, int>(RectanglePositions[rect].Key, RectanglePositions[rect].Value);
            RectanglePositions[rect] = new KeyValuePair<int, int>(previousEmpty.Key, previousEmpty.Value);

            //gets the associated rendertransform of the tile into something usable for translate transforms
            TranslateTransform moveRectangle = (TranslateTransform)rect.RenderTransform;

            //translates, multiplied by integer to determine direction for negative/positive movement
            moveRectangle.X += (TRANSLATE_DISTANCE * direction);

            //find new active tiles
            CheckPositions();

        }




        // METHOD           :   MoveOnYAxis
        // DESCRIPTION      :   Calculates the movement necessary for the tile to move on the X axis.
        //                      can go left or right depending on parameter passed in
        //
        // PARAMETERS       :
        //  Rectangle rect  :   contains the object ref to rectangle to be moved
        //  int direction   :   contains direction to move, -1 is up +1 is down
        //
        // RETURNS          :
        //  Nothing.
        //
        private void MoveOnYAxis(Rectangle rect, int direction)
        {
            //keeping old empty position
            KeyValuePair<int, int> previousEmpty = new KeyValuePair<int, int>(RectanglePositions[empty].Key,
                                                                            RectanglePositions[empty].Value);

            //swapping the positions of the empty and tile to move in the virtual grid within the collection
            RectanglePositions[empty] = new KeyValuePair<int, int>(RectanglePositions[rect].Key, RectanglePositions[rect].Value);
            RectanglePositions[rect] = new KeyValuePair<int, int>(previousEmpty.Key, previousEmpty.Value);

            //gets the associated rendertransform of the tile into something usable for translate transforms
            TranslateTransform moveRectangle = (TranslateTransform)rect.RenderTransform;

            //translates, multiplied by integer to determine direction for negative/positive movement
            moveRectangle.Y += (TRANSLATE_DISTANCE * direction);

            //find new active tiles
            CheckPositions();
        }




        // METHOD           :   CheckPositions
        // DESCRIPTION      :   This method checks the positions of all the tiles around the empty position
        //                      to determine which tiles need to be active and in what direction.
        //
        // PARAMETERS       :
        //  Nothing.
        //
        // RETURNS          :
        //  Nothing.
        //
        private void CheckPositions()
        {
            //getting the empty position in the virtual grid for reference
            int emptyRow = RectanglePositions[empty].Key;
            int emptyColumn = RectanglePositions[empty].Value;

            //clearing the list of active tiles
            ActiveRectangleDirectionOfMovement.Clear();

            Boolean win = false;

            //only checks tiles in winning positions if flag is set
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

                //win state
                if (win == true)
                {
                    gameWon();
                }
            }


            //removing all events from all tiles
            foreach (Rectangle r in rectArray)
            {
                r.PointerPressed -= Rectangle_PointerPressed_MoveLeft;
                r.PointerPressed -= Rectangle_PointerPressed_MoveRight;
                r.PointerPressed -= Rectangle_PointerPressed_MoveUp;
                r.PointerPressed -= Rectangle_PointerPressed_MoveDown;
            }

            //getting the adjacent active tiles to the empty space
            KeyValuePair<int, int> left = new KeyValuePair<int, int>(emptyRow, emptyColumn - 1);
            KeyValuePair<int, int> right = new KeyValuePair<int, int>(emptyRow, emptyColumn + 1);
            KeyValuePair<int, int> down = new KeyValuePair<int, int>(emptyRow + 1, emptyColumn);
            KeyValuePair<int, int> up = new KeyValuePair<int, int>(emptyRow - 1, emptyColumn);
            

            if (win == false)
            {
                //looking for a match for existing tiles to the adjacent positions to empty and setting
                //appropriate events as well as adding to the active tile collection
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




        // METHOD           :   RandomizeRectangles
        // DESCRIPTION      :   Moves a random active tile when called.
        //
        // PARAMETERS       :
        //  Nothing.
        //
        // RETURNS          :
        //  Nothing.
        //
        void RandomizeRectangles()
        {
            Random r = new Random(DateTime.Now.Millisecond);
            int selection = r.Next(0, ActiveRectangleDirectionOfMovement.Count);
            Rectangle rectSelect = ActiveRectangleDirectionOfMovement[selection].Key;


            //uses the active tile collection to randomly select one of the valid movable tiles
            //and moves it
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




        // METHOD           :   startGameButton_Click
        // DESCRIPTION      :   This event is fired when the start game button is clicked to start
        //                      the game.
        //
        // PARAMETERS       :
        //  object sender               :   self reference calling event
        //  PointerRoutedEventArgs e    :   event fired on click
        //
        // RETURNS          :
        //  Nothing.
        //
        private void startGameButton_Click(object sender, RoutedEventArgs e)
        {
            //disable game active so randomize doesn't unintentionally win
            _gameActive = false;
            usernameError.Text = "";

            //blank username check
            if (usernameInput.Text == String.Empty || usernameInput.Text == null)
            {
                usernameError.Text = "NAME CANNOT BE BLANK";
            }

            //valid, randomizes the game field, sets active tiles and starts the timer,
            //disable username editing
            else
            {
                localSettings.Values["userName"] = usernameInput.Text;


                CheckPositions();

                for (int i = 0; i < RANDOMIZE_COUNT; ++i)
                {
                    RandomizeRectangles();
                }
                _gameActive = true;
                Counter = 0;
                labelCounter.Text = "";
                tmr.Start();
                usernameInput.IsEnabled = false;

            }

        }

        /* 
         * METHOD      : gameWon()
         * DESCRIPTION :
         *      This method is called when the game winning state is called. The timer is stopped, and the top 10
         *      leader board ranks along with the current rank are added to a list with a string and int key value
         *      pair. The list is sorted, and the local settings/leader board is re displayed in the correct ranking.
         * PARAMETERS  :
         *      void : void
         * RETURNS     :
         *      void : void
         */
        private void gameWon()
        {
            localSettings.Values["gameState"] = WIN;
            tmr.Stop();

            List<KeyValuePair<string, int>> leaderBoard = new List<KeyValuePair<string, int>>();

            //Add the current attempt to the list
            string currentUser = (string)localSettings.Values["userName"];
            int currentTime = Counter;
            leaderBoard.Add(new KeyValuePair<string, int>(currentUser, currentTime));

            //Add the top 10 leader board entries to the list
            for (int i = 1; i < 11; i++)
            {
                string playerName = (string)localSettings.Values["p" + i.ToString()];
                int playerScore = (int)localSettings.Values[i.ToString()];
                leaderBoard.Add(new KeyValuePair<string, int>(playerName, playerScore));
            }

            //REFERENCE:
            //Bambrick, L. (2008, August, 2). How do you sort a dictionary by value?
            //https://stackoverflow.com/questions/289/how-do-you-sort-a-dictionary-by-value
            //Sort the list with the top 10 entries, and the current attempt
            var sortedList = leaderBoard.OrderBy(d => d.Value).ToList();
            leaderboard.Items.Clear();

            //Iterate over the first 10 entries for the sorted list
            for (int i = 1; i < 11; i++)
            {
                string playerName = sortedList[i - 1].Key;
                int playerScore = sortedList[i - 1].Value;
                string dummyScore = "";

                //Update the top 10 leader board ranks in local settings
                localSettings.Values["p" + i.ToString()] = playerName;
                localSettings.Values[i.ToString()] = playerScore;

                //If the top 10 are still placeholders with No users, do not display the Int32.MaxValue
                if(playerScore == Int32.MaxValue)
                {
                    dummyScore = " ";
                }
                else
                {
                    dummyScore = playerScore.ToString();
                }

                //Add the user to the leader board
                leaderboard.Items.Add(i + ". " + playerName + ": " + dummyScore);
            }
        }
    }
}