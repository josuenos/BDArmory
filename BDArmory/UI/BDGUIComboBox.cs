using UnityEngine;

namespace BDArmory.UI
{
    public class BDGUIComboBox
    {
        private static bool forceToUnShow = false;
        private static int useControlID = -1;
        private bool isClickedComboButton = false;
        public bool isOpen { get { return isClickedComboButton; } }
        private int selectedItemIndex = -1;

        private Rect rect;
        private Rect buttonRect;
        private GUIContent buttonContent;
        private GUIContent[] listContent;
        private GUIStyle listStyle;
        private Vector2 scrollViewVector;
        private float comboxbox_height;
        private bool thisTriggeredForceClose = false;
        private bool forceCloseNow = false;

        public BDGUIComboBox(Rect rect, Rect buttonRect, GUIContent buttonContent, GUIContent[] listContent, float combo_height, GUIStyle listStyle)
        {
            this.rect = rect;
            this.buttonRect = buttonRect;
            this.buttonContent = buttonContent;
            this.listContent = listContent;
            this.listStyle = listStyle;
            this.listStyle.active.textColor = Color.black;
            this.listStyle.hover.textColor = Color.black;
            this.comboxbox_height = combo_height;
        }

        public int Show()
        {
            bool done = false;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (forceToUnShow && !thisTriggeredForceClose) // Close all other comboboxes
            { isClickedComboButton = false; }
            if (forceCloseNow)
            {
                forceToUnShow = false;
                forceCloseNow = false;
            }
            if (thisTriggeredForceClose)
            {
                thisTriggeredForceClose = false;
                forceCloseNow = true;
            }
            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.MouseUp:
                    if (isClickedComboButton)
                    { done = true; }
                    break;
            }

            if (GUI.Button(buttonRect, buttonContent, BDArmorySetup.BDGuiSkin.button))
            {
                if (useControlID == -1)
                { useControlID = controlID; }
                if (useControlID != controlID)
                {
                    if (isClickedComboButton)
                    {
                        forceToUnShow = true;
                        thisTriggeredForceClose = true;
                    }
                    useControlID = controlID;
                }
                isClickedComboButton = true;
            }

            if (isClickedComboButton)
            {
                float items_height = listStyle.CalcHeight(listContent[0], 1.0f) * (listContent.Length + 5);
                Rect listRect = new Rect(rect.x + 5, rect.y + listStyle.CalcHeight(listContent[0], 1.0f), rect.width - 20f, items_height);

                scrollViewVector = GUI.BeginScrollView(new Rect(rect.x, rect.y + rect.height, rect.width + 10f, comboxbox_height), scrollViewVector,
                                                        new Rect(rect.x, rect.y, rect.width - 10, items_height + rect.height), false, false, BDArmorySetup.BDGuiSkin.horizontalScrollbar, BDArmorySetup.BDGuiSkin.verticalScrollbar);

                GUI.Box(new Rect(rect.x, rect.y, rect.width - 10, items_height + rect.height), "", BDArmorySetup.BDGuiSkin.window);

                if (selectedItemIndex != (selectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 2, listStyle)))
                {
                    if (selectedItemIndex > -1) buttonContent.text = listContent[selectedItemIndex].text;
                    done = true;
                }

                GUI.EndScrollView();
            }

            if (done)
            { isClickedComboButton = false; }

            return selectedItemIndex;
        }

        public void UpdateRect(Rect r)
        {
            if (r == rect) return;
            buttonRect.x += r.x - rect.x;
            buttonRect.y += r.y - rect.y;
            rect = r;
        }

        public int SelectedItemIndex
        {
            get
            {
                return selectedItemIndex;
            }
            set
            {
                selectedItemIndex = value;
            }
        }
    }
}
