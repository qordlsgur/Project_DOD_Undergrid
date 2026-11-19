using Prohect_DOD_Undergird.DOD.World;
using Prohect_DOD_Undergird.World;
using UnityEngine;
using UnityEngine.UI;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// PlayerInventory의 실제 슬롯을 uGUI로 표시하며 화면 표시 데이터만 갱신합니다.
    /// 슬롯 수량이나 아이템 상태를 별도로 보유하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public sealed class PlayerInventoryUI : MonoBehaviour
    {
        private const int GridColumnCount = 8;
        private const int CopperOreItemId = 1001;
        private const int IronOreItemId = 1002;
        private const float TargetSlotWidth = 136f;
        private const float TargetSlotHeight = 100f;
        private const float TargetGridSpacing = 10f;
        private static readonly Color32 CopperOreIconColor = new Color32(0xE8, 0x75, 0x32, 0xFF);
        private static readonly Color32 IronOreIconColor = new Color32(0xC0, 0xC0, 0xC0, 0xFF);

        private sealed class SlotView
        {
            public RectTransform rectTransform;
            public Image background;
            public Image icon;
            public Text amount;
        }

        private PlayerInventory playerInventory;
        private PlayerInputHandler inputHandler;
        private Canvas inventoryCanvas;
        private GameObject inventoryPanel;
        private RectTransform gridTransform;
        private GridLayoutGroup slotGrid;
        private RectTransform tooltipTransform;
        private Text tooltipText;
        private SlotView[] slotViews;
        private Font uiFont;
        private bool isOpen;
        private int hoveredSlotIndex = -1;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            playerInventory = GetComponent<PlayerInventory>();
            inputHandler = GetComponent<PlayerInputHandler>();

            if (!playerInventory.IsInitialized)
            {
                playerInventory.Initialize();
            }

            if (playerInventory.Inventory == null)
            {
                return;
            }

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateCanvas();
            CreateInventoryPanel();
            CreateSlotViews();
            CreateTooltip();
            RefreshAllSlots();
            Canvas.ForceUpdateCanvases();
            RefreshResponsiveLayout();
            CacheScreenSize();
            SetOpen(false);
        }

        private void LateUpdate()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                // 해상도가 바뀐 경우에만 Canvas의 새 크기를 반영해 슬롯 크기를 다시 계산합니다.
                Canvas.ForceUpdateCanvases();
                RefreshResponsiveLayout();
                CacheScreenSize();
            }

            if (isOpen)
            {
                UpdateHoveredSlot();
            }
        }

        private void OnEnable()
        {
            if (inputHandler == null)
            {
                inputHandler = GetComponent<PlayerInputHandler>();
            }

            if (inputHandler != null)
            {
                inputHandler.InventoryToggleRequested += ToggleInventory;
            }

            if (playerInventory != null && playerInventory.Inventory != null)
            {
                playerInventory.Inventory.SlotChanged += OnSlotChanged;
            }
        }

        private void OnDisable()
        {
            if (inputHandler != null)
            {
                inputHandler.InventoryToggleRequested -= ToggleInventory;
            }

            if (playerInventory != null && playerInventory.Inventory != null)
            {
                playerInventory.Inventory.SlotChanged -= OnSlotChanged;
            }

            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (inventoryCanvas != null)
            {
                Destroy(inventoryCanvas.gameObject);
            }
        }

        /// <summary>PlayerInputHandler의 Tab 이벤트와 테스트에서 공통으로 사용하는 토글 동작입니다.</summary>
        public void ToggleInventory()
        {
            if (inventoryPanel != null)
            {
                SetOpen(!isOpen);
            }
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(open);
            }

            if (!open)
            {
                hoveredSlotIndex = -1;
                if (tooltipTransform != null)
                {
                    tooltipTransform.gameObject.SetActive(false);
                }
            }

            if (open)
            {
                RefreshAllSlots();
            }
        }

        private void CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Player Inventory Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            inventoryCanvas = canvasObject.GetComponent<Canvas>();
            inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            inventoryCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void CreateInventoryPanel()
        {
            RectTransform panelTransform = CreateRectTransform(
                "Inventory Panel",
                inventoryCanvas.transform,
                new Vector2(0.12f, 0.12f),
                new Vector2(0.88f, 0.88f));
            Image panelImage = panelTransform.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.055f, 0.065f, 0.085f, 0.96f);
            inventoryPanel = panelTransform.gameObject;

            RectTransform titleTransform = CreateRectTransform(
                "Title",
                panelTransform,
                new Vector2(0.05f, 0.83f),
                new Vector2(0.95f, 0.96f));
            Text title = titleTransform.gameObject.AddComponent<Text>();
            ConfigureText(title, 30, TextAnchor.MiddleLeft);
            title.text = "Inventory";

            gridTransform = CreateRectTransform(
                "Slot Grid",
                panelTransform,
                new Vector2(0.05f, 0.06f),
                new Vector2(0.95f, 0.78f));
            slotGrid = gridTransform.gameObject.AddComponent<GridLayoutGroup>();
            slotGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            slotGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            slotGrid.childAlignment = TextAnchor.UpperCenter;
            slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            slotGrid.constraintCount = GridColumnCount;
        }

        private void CreateSlotViews()
        {
            SlotInventory inventory = playerInventory.Inventory;
            slotViews = new SlotView[inventory.Capacity];
            for (int i = 0; i < slotViews.Length; i++)
            {
                RectTransform slotTransform = CreateRectTransform(
                    $"Slot {i + 1}",
                    gridTransform,
                    Vector2.zero,
                    Vector2.one);
                Image background = slotTransform.gameObject.AddComponent<Image>();
                background.color = new Color(0.16f, 0.18f, 0.22f, 1f);

                RectTransform iconTransform = CreateRectTransform(
                    "Icon",
                    slotTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                iconTransform.pivot = new Vector2(0.5f, 0.5f);
                iconTransform.anchoredPosition = Vector2.zero;
                iconTransform.sizeDelta = Vector2.zero;
                Image icon = iconTransform.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                RectTransform amountTransform = CreateRectTransform(
                    "Amount",
                    slotTransform,
                    new Vector2(0.34f, 0.03f),
                    new Vector2(0.97f, 0.32f));
                Text amount = amountTransform.gameObject.AddComponent<Text>();
                ConfigureText(amount, 26, TextAnchor.MiddleRight);
                amount.resizeTextForBestFit = true;
                amount.resizeTextMinSize = 12;
                amount.resizeTextMaxSize = 28;
                amount.color = new Color(1f, 0.9f, 0.55f, 1f);
                amount.fontStyle = FontStyle.Bold;
                Outline amountOutline = amount.gameObject.AddComponent<Outline>();
                amountOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                amountOutline.effectDistance = new Vector2(1.5f, -1.5f);
                amountTransform.SetAsLastSibling();

                slotViews[i] = new SlotView
                {
                    rectTransform = slotTransform,
                    background = background,
                    icon = icon,
                    amount = amount
                };
            }
        }

        private void CreateTooltip()
        {
            tooltipTransform = CreateRectTransform(
                "Inventory Tooltip",
                inventoryCanvas.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            tooltipTransform.pivot = new Vector2(0f, 1f);
            tooltipTransform.sizeDelta = new Vector2(300f, 48f);

            tooltipText = tooltipTransform.gameObject.AddComponent<Text>();
            ConfigureText(tooltipText, 22, TextAnchor.MiddleLeft);
            tooltipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            tooltipText.color = new Color(1f, 0.94f, 0.76f, 1f);

            Outline tooltipOutline = tooltipText.gameObject.AddComponent<Outline>();
            tooltipOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            tooltipOutline.effectDistance = new Vector2(1.5f, -1.5f);
            tooltipTransform.gameObject.SetActive(false);
        }

        private void UpdateHoveredSlot()
        {
            int hoveredIndex = FindHoveredSlotIndex(Input.mousePosition);
            if (hoveredIndex != hoveredSlotIndex)
            {
                hoveredSlotIndex = hoveredIndex;
                if (hoveredSlotIndex >= 0)
                {
                    RefreshTooltipContent(hoveredSlotIndex);
                }
                else
                {
                    tooltipTransform.gameObject.SetActive(false);
                }
            }

            if (hoveredSlotIndex >= 0 && tooltipTransform.gameObject.activeSelf)
            {
                PositionTooltip(Input.mousePosition);
            }
        }

        private int FindHoveredSlotIndex(Vector2 screenPosition)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                // Screen Space Overlay는 카메라 없이 화면 좌표와 슬롯 RectTransform을 비교할 수 있습니다.
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        slotViews[i].rectTransform,
                        screenPosition,
                        null))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RefreshTooltipContent(int slotIndex)
        {
            if (!playerInventory.TryGetSlot(slotIndex, out InventorySlot slot) || slot.IsEmpty)
            {
                tooltipTransform.gameObject.SetActive(false);
                return;
            }

            string itemName = playerInventory.TryGetItemData(slot.ItemId, out InventoryItemData itemData)
                ? itemData.DisplayName
                : $"Item {slot.ItemId.Value}";
            tooltipText.text = $"{itemName} ({slot.Amount})";
            tooltipTransform.gameObject.SetActive(true);
            PositionTooltip(Input.mousePosition);
        }

        private void PositionTooltip(Vector2 screenPosition)
        {
            RectTransform canvasTransform = inventoryCanvas.transform as RectTransform;
            // 화면 좌표를 Canvas 로컬 좌표로 바꿔 해상도 배율과 무관하게 커서 옆에 배치합니다.
            if (canvasTransform == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasTransform,
                    screenPosition,
                    null,
                    out Vector2 localPosition))
            {
                return;
            }

            Rect canvasBounds = canvasTransform.rect;
            Rect tooltipBounds = tooltipTransform.rect;
            float x = Mathf.Clamp(
                localPosition.x + 18f,
                canvasBounds.xMin + 8f,
                canvasBounds.xMax - tooltipBounds.width - 8f);
            float y = Mathf.Clamp(
                localPosition.y - 22f,
                canvasBounds.yMin + tooltipBounds.height + 8f,
                canvasBounds.yMax - 8f);
            tooltipTransform.anchoredPosition = new Vector2(x, y);
        }

        private void RefreshResponsiveLayout()
        {
            if (slotGrid == null || gridTransform == null || slotViews == null || slotViews.Length == 0)
            {
                return;
            }

            int rowCount = Mathf.CeilToInt((float)slotViews.Length / GridColumnCount);
            float availableWidth = gridTransform.rect.width;
            float availableHeight = gridTransform.rect.height;

            // 간격도 사용 가능한 크기에 맞춰 줄이고, 셀은 Grid 내부 크기를 넘지 않게 제한합니다.
            float horizontalSpacing = Mathf.Min(TargetGridSpacing, availableWidth / (GridColumnCount * 2f));
            float verticalSpacing = Mathf.Min(TargetGridSpacing, availableHeight / (rowCount * 2f));
            float cellWidth = Mathf.Min(
                TargetSlotWidth,
                (availableWidth - horizontalSpacing * (GridColumnCount - 1)) / GridColumnCount);
            float cellHeight = Mathf.Min(
                TargetSlotHeight,
                (availableHeight - verticalSpacing * (rowCount - 1)) / rowCount);

            slotGrid.spacing = new Vector2(horizontalSpacing, verticalSpacing);
            slotGrid.cellSize = new Vector2(Mathf.Max(1f, cellWidth), Mathf.Max(1f, cellHeight));

            int amountFontSize = Mathf.RoundToInt(
                Mathf.Clamp(Mathf.Min(cellWidth * 0.2f, cellHeight * 0.24f), 12f, 28f));
            for (int i = 0; i < slotViews.Length; i++)
            {
                // 기존 스트레치 앵커가 차지하던 비율을 유지하면서 중앙 기준 크기로 변환합니다.
                slotViews[i].icon.rectTransform.sizeDelta = new Vector2(cellWidth * 0.6f, cellHeight * 0.58f);
                slotViews[i].amount.fontSize = amountFontSize;
                slotViews[i].amount.resizeTextMaxSize = amountFontSize;
            }
        }

        private void CacheScreenSize()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        private void RefreshAllSlots()
        {
            if (slotViews == null)
            {
                return;
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                RefreshSlot(i);
            }
        }

        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= slotViews.Length ||
                !playerInventory.TryGetSlot(index, out InventorySlot slot))
            {
                return;
            }

            SlotView view = slotViews[index];
            if (slot.IsEmpty)
            {
                view.icon.sprite = null;
                view.icon.enabled = false;
                view.icon.color = Color.white;
                view.amount.text = string.Empty;
                view.background.color = new Color(0.12f, 0.13f, 0.16f, 1f);
                return;
            }

            if (playerInventory.TryGetItemData(slot.ItemId, out InventoryItemData itemData))
            {
                view.icon.sprite = itemData.Icon;
                view.icon.enabled = itemData.Icon != null;
                view.icon.color = GetItemIconColor(itemData.Id);
            }
            else
            {
                view.icon.sprite = null;
                view.icon.enabled = false;
                view.icon.color = Color.white;
            }

            view.amount.text = slot.Amount.ToString();
            view.background.color = new Color(0.2f, 0.22f, 0.26f, 1f);
        }

        private static Color GetItemIconColor(ItemId itemId)
        {
            // 광물 타일과 같은 색상 규칙을 사용해 공통 임시 Sprite만으로 종류를 구분합니다.
            if (itemId.Value == CopperOreItemId)
            {
                return CopperOreIconColor;
            }

            if (itemId.Value == IronOreItemId)
            {
                return IronOreIconColor;
            }

            return Color.white;
        }

        private void OnSlotChanged(int index, InventorySlot slot)
        {
            if (isOpen)
            {
                RefreshSlot(index);
                if (index == hoveredSlotIndex)
                {
                    RefreshTooltipContent(index);
                }
            }
        }

        private RectTransform CreateRectTransform(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var uiObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = uiObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        private void ConfigureText(Text text, int fontSize, TextAnchor alignment)
        {
            text.font = uiFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
