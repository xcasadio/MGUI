using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.MiniGame
{
    public class MiniGame : Game, IObservableUpdate
    {
        private enum EquipmentSlot
        {
            None,
            Weapon,
            Armor,
            Accessory,
        }

        private enum TradeMode
        {
            Buy,
            Sell,
        }

        private sealed class ItemDefinition
        {
            public string Name { get; }
            public string Description { get; }
            public int BuyPrice { get; }
            public int SellPrice { get; }
            public EquipmentSlot EquipmentSlot { get; }
            public float HealthRestore { get; }
            public float MagicRestore { get; }
            public float BonusHealth { get; }
            public float BonusMagic { get; }
            public float BonusMoveSpeed { get; }
            public bool CanSell { get; }
            public bool IsConsumable => HealthRestore > 0f || MagicRestore > 0f || Name == "Antidote";
            public bool IsEquippable => EquipmentSlot != EquipmentSlot.None;

            public ItemDefinition(string name, string description, int buyPrice, int sellPrice, EquipmentSlot equipmentSlot = EquipmentSlot.None,
                float healthRestore = 0f, float magicRestore = 0f, float bonusHealth = 0f, float bonusMagic = 0f,
                float bonusMoveSpeed = 0f, bool canSell = true)
            {
                Name = name;
                Description = description;
                BuyPrice = buyPrice;
                SellPrice = sellPrice;
                EquipmentSlot = equipmentSlot;
                HealthRestore = healthRestore;
                MagicRestore = magicRestore;
                BonusHealth = bonusHealth;
                BonusMagic = bonusMagic;
                BonusMoveSpeed = bonusMoveSpeed;
                CanSell = canSell;
            }
        }

        private sealed class TradeEntry
        {
            public ItemDefinition Item { get; }
            public int Price { get; }
            public int Quantity { get; }
            public bool IsVendorStock { get; }

            public TradeEntry(ItemDefinition item, int price, int quantity, bool isVendorStock)
            {
                Item = item;
                Price = price;
                Quantity = quantity;
                IsVendorStock = isVendorStock;
            }
        }

        private sealed class QuickSlot
        {
            public string ItemName { get; }
            public string DisplayName { get; }
            public string ShortcutHint { get; }

            public QuickSlot(string itemName, string displayName, string shortcutHint)
            {
                ItemName = itemName;
                DisplayName = displayName;
                ShortcutHint = shortcutHint;
            }
        }

        public sealed class ShopOffer
        {
            public string Name { get; }
            public int Price { get; }

            public ShopOffer(string name, int price)
            {
                Name = name;
                Price = price;
            }
        }

        public sealed class CircleEntity
        {
            public Vector2 Position { get; set; }
            public float Radius { get; set; }
            public Color Color { get; set; }
            public float MovementSpeed { get; set; }

            public CircleEntity(Vector2 position, float radius, Color color, float movementSpeed = 0f)
            {
                Position = position;
                Radius = radius;
                Color = color;
                MovementSpeed = movementSpeed;
            }
        }

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _circleTexture;

        private MainRenderer _mguiRenderer;
        private MGDesktop _desktop;
        private InputRouter _inputRouter;
        private MGWindow _hudWindow;
        private MGWindow _helpWindow;
        private MGWindow _inventoryWindow;
        private MGWindow _vendorDialogWindow;
        private MGWindow _shopWindow;
        private MGProgressBar _healthBar;
        private MGProgressBar _magicBar;
        private MGTextBlock _moneyText;
        private MGTextBlock _interactionText;
        private MGTextBlock _statusText;
        private MGTextBlock _quickSlotsText;
        private MGTextBlock _helpText;
        private MGTextBlock _inventorySelectionText;
        private MGTextBlock _vendorDialogText;
        private MGTextBlock _shopModeText;
        private MGTextBlock _shopMoneyText;
        private MGTextBlock _shopOwnedQuantityText;
        private MGTextBlock _shopSelectedItemText;
        private MGTextBlock _shopStatusText;
        private MGStackPanel _inventoryContent;
        private MGStackPanel _shopContent;
        private MGListBox<string> _inventoryOwnedItemsList;
        private MGListBox<TradeEntry> _shopOffersList;
        private MGButton _helpCloseButton;
        private MGButton _inventoryUsePotionButton;
        private MGButton _inventoryPrimaryActionButton;
        private MGButton _inventoryCloseButton;
        private MGButton _vendorBuyButton;
        private MGButton _vendorLeaveButton;
        private MGButton _shopModeToggleButton;
        private MGButton _shopBuyButton;
        private MGButton _shopLeaveButton;
        private TradeEntry _selectedTradeEntry;
        private string _selectedInventoryItemName;

        private readonly List<CircleEntity> _entities = [];
        private readonly List<QuickSlot> _quickSlots =
        [
            new QuickSlot("Potion", "Potion", "C/R or LB/RB, F/RT to use"),
            new QuickSlot("Potion de magie", "Mana Potion", "C/R or LB/RB, F/RT to use"),
            new QuickSlot("Antidote", "Antidote", "C/R or LB/RB, F/RT to use")
        ];
        private readonly Dictionary<string, ItemDefinition> _itemDefinitions = CreateItemDefinitions();
        private readonly List<string> _vendorOfferItemNames =
        [
            "Potion",
            "Potion de magie",
            "Antidote",
            "Élixir",
            "Cape du voyageur",
            "Dague du marchand",
            "Anneau du troc"
        ];
        private CircleEntity _player;
        private CircleEntity _vendor;
        private int _selectedQuickSlotIndex;
        private TradeMode _shopMode = TradeMode.Buy;

        private bool _isHelpOpen;
        private bool _isInventoryOpen;
        private bool _isVendorDialogOpen;
        private bool _isShopOpen;

        private const float BaseMaxHealth = 100f;
        private const float BaseMaxMagic = 100f;
        private const float BaseMoveSpeed = 240f;
        private float _playerHealth = 85f;
        private float _playerMagic = 60f;
        private int _playerMoney = 125;

        private readonly Dictionary<EquipmentSlot, string> _equippedItems = new()
        {
            [EquipmentSlot.Weapon] = "Épée en fer",
            [EquipmentSlot.Armor] = "Tunique de cuir",
            [EquipmentSlot.Accessory] = "Anneau de mana"
        };

        private readonly Dictionary<string, int> _ownedItems = new()
        {
            ["Potion"] = 5,
            ["Potion de magie"] = 3,
            ["Herbe médicinale"] = 4,
            ["Antidote"] = 2,
            ["Clé du marchand"] = 1
        };

        private const float VendorInteractionRadius = 90f;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public MiniGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
        }

        protected override void Initialize()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _mguiRenderer = new MainRenderer(new GameRenderHost<MiniGame>(this), new MonoGameRawInputSource());
            _desktop = new MGDesktop(_mguiRenderer);

            CreateHudWindow();
            CreateHelpWindow();
            CreateInventoryWindow();
            CreateVendorDialogWindow();
            CreateShopWindow();
            InitializeInputRouting();
            InitializeEntities();
            RecalculateDerivedStats();
            OpenHelpWindow();

            Window.ClientSizeChanged += (_, _) => HandleClientSizeChanged();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _circleTexture = CreateCircleTexture(128);
        }

        protected override void Update(GameTime gameTime)
        {
            PreviewUpdate?.Invoke(this, gameTime.TotalGameTime);

            DispatchRoutedInputActions(gameTime.TotalGameTime);

            KeyboardState currentKeyboardState = _mguiRenderer.Input.Keyboard.CurrentState;
            GamePadState currentGamePadState = _mguiRenderer.Input.GamePad.CurrentState;

            if (!IsGameplayMovementBlocked())
            {
                UpdatePlayer(gameTime, currentKeyboardState, currentGamePadState);
            }

            UpdateHud();
            _desktop.Update();

            base.Update(gameTime);

            EndUpdate?.Invoke(this, EventArgs.Empty);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);

            foreach (CircleEntity entity in _entities)
            {
                DrawEntity(entity);
            }

            _spriteBatch.End();

            _desktop.Draw();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            _circleTexture?.Dispose();
            _spriteBatch?.Dispose();

            base.UnloadContent();
        }

        public CircleEntity AddEntity(Vector2 position, float radius, Color color, float movementSpeed = 0f)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

            CircleEntity entity = new(position, radius, color, movementSpeed);
            _entities.Add(entity);
            return entity;
        }

        private void InitializeEntities()
        {
            Viewport viewport = GraphicsDevice.Viewport;
            _player = AddEntity(new Vector2(viewport.Width * 0.35f, viewport.Height * 0.5f), 22f, Color.DarkGreen, 240f);
            _vendor = AddEntity(new Vector2(viewport.Width * 0.7f, viewport.Height * 0.45f), 26f, Color.DarkBlue);
        }

        private void CreateHudWindow()
        {
            _hudWindow = new MGWindow(_desktop, 16, 16, 320, 180)
            {
                WindowStyle = WindowStyle.None,
                AllowsClickThrough = true,
                CanCloseWindow = false,
                IsTopmost = true,
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1)
            };
            _hudWindow.BackgroundBrush.SetAll(new Color(0, 0, 0, 180).AsFillBrush());
            _hudWindow.BorderBrush = new Color(24, 24, 24, 220).AsFillBrush().AsUniformBorderBrush();

            MGStackPanel content = new(_hudWindow, Orientation.Vertical)
            {
                PreferredWidth = 280,
                PreferredHeight = 150,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            content.TryAddChild(new MGTextBlock(_hudWindow, "Vie", Color.White));
            _healthBar = CreateHudBar(Color.IndianRed, new Color(70, 20, 20));
            content.TryAddChild(_healthBar);

            content.TryAddChild(new MGTextBlock(_hudWindow, "Magie", Color.White));
            _magicBar = CreateHudBar(Color.RoyalBlue, new Color(15, 30, 70));
            content.TryAddChild(_magicBar);

            _moneyText = new MGTextBlock(_hudWindow, string.Empty, Color.Gold, 16)
            {
                Margin = new Thickness(0, 4, 0, 0)
            };
            content.TryAddChild(_moneyText);

            _interactionText = new MGTextBlock(_hudWindow, string.Empty, Color.LightGreen, 14)
            {
                Margin = new Thickness(0, 6, 0, 0)
            };
            content.TryAddChild(_interactionText);

            _statusText = new MGTextBlock(_hudWindow, "Déplacez-vous, ouvrez l'inventaire avec I/Y et parlez au vendeur avec E/X.", Color.White, 13)
            {
                Margin = new Thickness(0, 4, 0, 0)
            };
            content.TryAddChild(_statusText);

            _quickSlotsText = new MGTextBlock(_hudWindow, string.Empty, Color.LightBlue, 13)
            {
                Margin = new Thickness(0, 6, 0, 0)
            };
            content.TryAddChild(_quickSlotsText);

            _hudWindow.SetContent(content);
            _desktop.Windows.Add(_hudWindow);
            UpdateHud();
        }

        private void CreateHelpWindow()
        {
            _helpWindow = new MGWindow(_desktop, 0, 0, 540, 360)
            {
                TitleText = "Mini-Game Input Routing Demo",
                IsTopmost = true,
            };
            _helpWindow.WindowClosed += (_, _) => _isHelpOpen = false;

            MGStackPanel content = new(_helpWindow, Orientation.Vertical)
            {
                PreferredWidth = 470,
                PreferredHeight = 300,
                Spacing = 10,
            };

            _helpText = new MGTextBlock(_helpWindow,
                "This sample demonstrates three routed layers: MGUI UI, an interactive HUD, and gameplay fallback.\n\n" +
                "World controls\n" +
                "- Move with WASD, arrows, or the left stick\n" +
                "- Press E or X near the merchant to start a conversation\n" +
                "- Press I or Y to open and close the inventory\n" +
                "- Press H or Back to reopen this help window\n\n" +
                "HUD controls\n" +
                "- Press C/R or LB/RB to cycle quick slots\n" +
                "- Press F or RT to use the selected quick item\n\n" +
                "Shop controls\n" +
                "- Navigate with Tab, arrows, D-Pad, or the left stick\n" +
                "- Press Enter or A to activate the focused control\n" +
                "- Press LB/RB while the shop is open to switch between buy and sell mode\n\n" +
                "Close this window to start playing.",
                Color.White,
                15)
            {
                WrapText = true,
            };
            content.TryAddChild(_helpText);

            _helpCloseButton = new MGButton(_helpWindow, _ => CloseHelpWindow())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _helpCloseButton.SetContent("Start Demo");
            content.TryAddChild(_helpCloseButton);

            _helpWindow.DefaultFocusElement = _helpCloseButton;
            _helpWindow.SetContent(content);
            PositionHelpWindow();
        }

        private MGProgressBar CreateHudBar(Color fillColor, Color backgroundColor)
        {
            MGProgressBar bar = new(_hudWindow, 0, 100, 0, 22, true)
            {
                PreferredWidth = 280,
                NumberFormat = "0",
                ValueDisplayFormat = MGProgressBar.RecommendedExactValueDisplayFormat
            };
            bar.CompletedBrush.SetAll(fillColor.AsFillBrush());
            bar.IncompleteBrush.SetAll(backgroundColor.AsFillBrush());
            return bar;
        }

        private void CreateInventoryWindow()
        {
            _inventoryWindow = new MGWindow(_desktop, 0, 0, 420, 320)
            {
                TitleText = "Inventaire",
                IsTopmost = true
            };
            _inventoryWindow.WindowClosed += (_, _) => _isInventoryOpen = false;

            _inventoryContent = new MGStackPanel(_inventoryWindow, Orientation.Vertical)
            {
                PreferredWidth = 360,
                PreferredHeight = 240,
                Spacing = 8
            };

            _inventoryWindow.SetContent(_inventoryContent);
            RefreshInventoryContent();
            PositionInventoryWindow();
        }

        private void CreateVendorDialogWindow()
        {
            _vendorDialogWindow = new MGWindow(_desktop, 0, 0, 380, 220)
            {
                TitleText = "Marchand ambulant",
                IsTopmost = true,
            };
            _vendorDialogWindow.WindowClosed += (_, _) => _isVendorDialogOpen = false;

            MGStackPanel content = new(_vendorDialogWindow, Orientation.Vertical)
            {
                PreferredWidth = 320,
                PreferredHeight = 160,
                Spacing = 10,
            };

            _vendorDialogText = new MGTextBlock(_vendorDialogWindow,
                "Bonjour voyageur. J'ai quelques provisions et une cape qui résiste bien à la pluie.",
                Color.White,
                15)
            {
                WrapText = true,
            };
            content.TryAddChild(_vendorDialogText);

            _vendorBuyButton = new MGButton(_vendorDialogWindow, _ => OpenShopWindow())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _vendorBuyButton.SetContent("Voir la boutique");
            content.TryAddChild(_vendorBuyButton);

            _vendorLeaveButton = new MGButton(_vendorDialogWindow, _ => CloseVendorDialog())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _vendorLeaveButton.SetContent("Repartir");
            content.TryAddChild(_vendorLeaveButton);

            _vendorDialogWindow.DefaultFocusElement = _vendorBuyButton;
            _vendorDialogWindow.SetContent(content);
            PositionVendorDialogWindow();
        }

        private void CreateShopWindow()
        {
            _shopWindow = new MGWindow(_desktop, 0, 0, 460, 340)
            {
                TitleText = "Achat",
                IsTopmost = true
            };
            _shopWindow.WindowClosed += (_, _) => _isShopOpen = false;

            _shopContent = new MGStackPanel(_shopWindow, Orientation.Vertical)
            {
                PreferredWidth = 400,
                PreferredHeight = 280,
                Spacing = 8
            };
            _shopModeText = new MGTextBlock(_shopWindow, string.Empty, Color.White, 18);
            _shopContent.TryAddChild(_shopModeText);

            MGDockPanel body = new(_shopWindow)
            {
                PreferredWidth = 410,
                PreferredHeight = 240,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _shopOffersList = new MGListBox<TradeEntry>(_shopWindow)
            {
                PreferredWidth = 240,
                PreferredHeight = 220,
                SelectionMode = ListBoxSelectionMode.Single,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Header = new MGTextBlock(_shopWindow, "Nom / Prix", Color.White, 14)
            };
            _shopOffersList.ItemTemplate = CreateShopOfferListItem;
            _shopOffersList.SelectionChanged += (_, _) =>
            {
                _selectedTradeEntry = _shopOffersList.SelectedValue;
                RefreshShopSelectionDetails();
            };

            MGStackPanel rightPanel = new(_shopWindow, Orientation.Vertical)
            {
                PreferredWidth = 150,
                PreferredHeight = 220,
                Spacing = 8,
                Margin = new Thickness(12, 0, 0, 0)
            };

            _shopMoneyText = new MGTextBlock(_shopWindow, string.Empty, Color.Gold, 16);
            rightPanel.TryAddChild(_shopMoneyText);

            _shopModeToggleButton = new MGButton(_shopWindow, _ => ToggleShopMode(1))
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PreferredWidth = 150
            };
            _shopModeToggleButton.SetContent("Switch to Sell Mode");
            rightPanel.TryAddChild(_shopModeToggleButton);

            _shopOwnedQuantityText = new MGTextBlock(_shopWindow, string.Empty, Color.White, 14);
            rightPanel.TryAddChild(_shopOwnedQuantityText);

            _shopSelectedItemText = new MGTextBlock(_shopWindow, string.Empty, Color.LightGray, 14);
            rightPanel.TryAddChild(_shopSelectedItemText);

            _shopBuyButton = new MGButton(_shopWindow, _ => ExecuteSelectedTrade())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PreferredWidth = 150
            };
            _shopBuyButton.SetContent("Acheter");
            rightPanel.TryAddChild(_shopBuyButton);

            _shopLeaveButton = new MGButton(_shopWindow, _ => CloseShopWindow())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PreferredWidth = 150
            };
            _shopLeaveButton.SetContent("Quitter la boutique");
            rightPanel.TryAddChild(_shopLeaveButton);

            _shopStatusText = new MGTextBlock(_shopWindow, "Sélectionnez un objet puis validez l'achat.", Color.LightGray, 14);
            rightPanel.TryAddChild(_shopStatusText);

            body.TryAddChild(_shopOffersList, Dock.Left);
            body.TryAddChild(rightPanel, Dock.Left);
            _shopContent.TryAddChild(body);

            _shopWindow.SetContent(_shopContent);
            _shopWindow.DefaultFocusElement = _shopOffersList;
            RefreshShopContent();
            RefreshShopSelectionDetails();
            PositionShopWindow();
        }

        private MGElement CreateShopOfferListItem(TradeEntry offer)
        {
            MGDockPanel row = new(_shopWindow)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                PreferredWidth = 220
            };

            MGTextBlock priceText = new(_shopWindow, $"{offer.Price} or", Color.Gold, 14)
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 0, 0)
            };

            string label = offer.IsVendorStock
                ? offer.Item.Name
                : $"{offer.Item.Name} x{offer.Quantity}";
            MGTextBlock nameText = new(_shopWindow, label, Color.White, 14)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            row.TryAddChild(priceText, Dock.Right);
            row.TryAddChild(nameText, Dock.Left);
            return row;
        }

        private void RefreshInventoryContent()
        {
            string previousSelection = _selectedInventoryItemName;
            _inventoryContent.TryRemoveAll();

            _inventoryContent.TryAddChild(new MGTextBlock(_inventoryWindow, "Équipement courant", Color.White, 18));

            foreach (var equippedItem in _equippedItems)
            {
                string slotName = GetEquipmentSlotDisplayName(equippedItem.Key);
                string itemName = equippedItem.Value;

                MGDockPanel equippedRow = new(_inventoryWindow)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    PreferredWidth = 360,
                };

                MGTextBlock equippedText = new(_inventoryWindow, $"- {slotName} : {itemName}", Color.LightGray, 14)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                equippedRow.TryAddChild(equippedText, Dock.Left);

                MGButton unequipButton = new(_inventoryWindow, _ => UnequipItem(equippedItem.Key))
                {
                    PreferredWidth = 110,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                unequipButton.SetContent($"Retirer {slotName}");
                equippedRow.TryAddChild(unequipButton, Dock.Right);

                _inventoryContent.TryAddChild(equippedRow);
            }

            _inventoryContent.TryAddChild(new MGTextBlock(_inventoryWindow, "Objets possédés", Color.White, 18)
            {
                Margin = new Thickness(0, 8, 0, 0)
            });

            MGDockPanel inventoryBody = new(_inventoryWindow)
            {
                PreferredWidth = 360,
                PreferredHeight = 170,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            _inventoryOwnedItemsList = new MGListBox<string>(_inventoryWindow)
            {
                PreferredWidth = 180,
                PreferredHeight = 160,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Header = new MGTextBlock(_inventoryWindow, "Sélection", Color.White, 14)
            };
            _inventoryOwnedItemsList.ItemTemplate = itemName => new MGTextBlock(_inventoryWindow, $"{itemName} x{GetOwnedQuantity(itemName)}", Color.White, 14);
            _inventoryOwnedItemsList.SelectionChanged += (_, _) =>
            {
                _selectedInventoryItemName = _inventoryOwnedItemsList.SelectedValue;
                RefreshInventorySelectionDetails();
            };

            MGStackPanel inventoryRightPanel = new(_inventoryWindow, Orientation.Vertical)
            {
                Spacing = 6,
                Margin = new Thickness(12, 0, 0, 0),
                PreferredWidth = 160,
                PreferredHeight = 160,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _inventorySelectionText = new MGTextBlock(_inventoryWindow, string.Empty, Color.LightGray, 14)
            {
                WrapText = true,
            };
            inventoryRightPanel.TryAddChild(_inventorySelectionText);

            _inventoryPrimaryActionButton = new MGButton(_inventoryWindow, _ => ExecuteInventorySelectionAction())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _inventoryPrimaryActionButton.SetContent("Utiliser");
            inventoryRightPanel.TryAddChild(_inventoryPrimaryActionButton);

            _inventoryUsePotionButton = new MGButton(_inventoryWindow, _ => UseOwnedPotion("Potion", 25f))
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _inventoryUsePotionButton.SetContent($"Boire une potion rapide x{GetOwnedQuantity("Potion")}");
            inventoryRightPanel.TryAddChild(_inventoryUsePotionButton);

            _inventoryCloseButton = new MGButton(_inventoryWindow, _ => ToggleInventory())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _inventoryCloseButton.SetContent("Fermer l'inventaire");
            inventoryRightPanel.TryAddChild(_inventoryCloseButton);

            inventoryBody.TryAddChild(_inventoryOwnedItemsList, Dock.Left);
            inventoryBody.TryAddChild(inventoryRightPanel, Dock.Right);

            _inventoryContent.TryAddChild(inventoryBody);

            List<string> ownedNames = _ownedItems.Keys.OrderBy(x => x).ToList();
            _inventoryOwnedItemsList.SetItemsSource(ownedNames);
            if (ownedNames.Count > 0)
            {
                _selectedInventoryItemName = ownedNames.Contains(previousSelection) ? previousSelection : ownedNames[0];
                _inventoryOwnedItemsList.SelectedValue = _selectedInventoryItemName;
            }
            else
            {
                _selectedInventoryItemName = null;
            }

            RefreshInventorySelectionDetails();
            _inventoryWindow.DefaultFocusElement = _inventoryOwnedItemsList;
        }

        private void RefreshShopContent()
        {
            _shopMoneyText?.SetText($"Votre argent : {_playerMoney} or");
            _shopModeText?.SetText(_shopMode == TradeMode.Buy ? "Acheter au marchand" : "Revendre au marchand");
            _shopModeToggleButton?.SetContent(_shopMode == TradeMode.Buy ? "Switch to Sell Mode" : "Switch to Buy Mode");
            RefreshTradeEntries();
        }

        private void RefreshShopSelectionDetails()
        {
            if (_selectedTradeEntry == null)
            {
                _shopOwnedQuantityText?.SetText("Quantité : 0");
                _shopSelectedItemText?.SetText("Aucun objet sélectionné.");
                _shopBuyButton?.SetContent(_shopMode == TradeMode.Buy ? "Acheter" : "Vendre");
                return;
            }

            ItemDefinition definition = _selectedTradeEntry.Item;
            int quantityOwned = GetOwnedQuantity(definition.Name);
            _shopOwnedQuantityText?.SetText($"Quantité : {quantityOwned}");
            _shopSelectedItemText?.SetText($"Objet : {definition.Name}\n{definition.Description}");
            _shopBuyButton?.SetContent(_shopMode == TradeMode.Buy
                ? $"Acheter ({_selectedTradeEntry.Price} or)"
                : $"Vendre ({_selectedTradeEntry.Price} or)");
        }

        private void UpdateHud()
        {
            _healthBar.Maximum = CurrentMaxHealth;
            _healthBar.Value = Math.Clamp(_playerHealth, 0f, CurrentMaxHealth);

            _magicBar.Maximum = CurrentMaxMagic;
            _magicBar.Value = Math.Clamp(_playerMagic, 0f, CurrentMaxMagic);

            _moneyText.SetText($"Argent : {_playerMoney}");
            _interactionText.SetText(GetInteractionPrompt(), true);
            _quickSlotsText.SetText(GetQuickSlotsSummary(), true);
            RefreshShopContent();
        }

        private string GetQuickSlotsSummary()
        {
            List<string> parts = new();
            for (int i = 0; i < _quickSlots.Count; i++)
            {
                QuickSlot slot = _quickSlots[i];
                string marker = i == _selectedQuickSlotIndex ? ">" : " ";
                parts.Add($"{marker} {slot.DisplayName} x{GetOwnedQuantity(slot.ItemName)}");
            }

            return "Raccourcis HUD\n" + string.Join("\n", parts);
        }

        private string GetInteractionPrompt()
        {
            if (_isHelpOpen)
            {
                return "Help open: press H or Back to close it, or use the Start Demo button.";
            }

            if (_isShopOpen)
            {
                return "Boutique ouverte: Tab/Fleches/Entree pour naviguer, I ou Y pour fermer, LB/RB pour acheter ou vendre.";
            }

            if (_isVendorDialogOpen)
            {
                return "Dialogue vendeur: Tab/Fleches/Entree pour choisir, I ou Y pour fermer.";
            }

            if (_isInventoryOpen)
            {
                return "Inventaire ouvert: utilisez les boutons ou I/Y pour fermer.";
            }

            if (IsHudContextActive())
            {
                return "HUD: C/R ou LB/RB pour changer d'objet rapide, F ou RT pour l'utiliser, H ou Back pour l'aide.";
            }

            if (IsPlayerNearVendor())
            {
                return "Pres du vendeur: E ou X pour parler, I ou Y pour l'inventaire, H ou Back pour l'aide.";
            }

            return "Deplacement: WASD/Fleches ou stick. Inventaire: I/Y. Help: H/Back.";
        }

        private void UpdatePlayer(GameTime gameTime, KeyboardState keyboardState, GamePadState gamePadState)
        {
            Vector2 movement = GetKeyboardMovement(keyboardState) + GetGamePadMovement(gamePadState);
            if (movement.LengthSquared() > 1f)
            {
                movement.Normalize();
            }

            _player.Position += movement * _player.MovementSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            ClampEntityToViewport(_player);
        }

        private static Vector2 GetKeyboardMovement(KeyboardState keyboardState)
        {
            Vector2 movement = Vector2.Zero;

            if (keyboardState.IsKeyDown(Keys.Left) || keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Q))
            {
                movement.X -= 1f;
            }

            if (keyboardState.IsKeyDown(Keys.Right) || keyboardState.IsKeyDown(Keys.D))
            {
                movement.X += 1f;
            }

            if (keyboardState.IsKeyDown(Keys.Up) || keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Z))
            {
                movement.Y -= 1f;
            }

            if (keyboardState.IsKeyDown(Keys.Down) || keyboardState.IsKeyDown(Keys.S))
            {
                movement.Y += 1f;
            }

            return movement;
        }

        private static Vector2 GetGamePadMovement(GamePadState gamePadState)
        {
            if (!gamePadState.IsConnected)
            {
                return Vector2.Zero;
            }

            Vector2 movement = new(gamePadState.ThumbSticks.Left.X, -gamePadState.ThumbSticks.Left.Y);

            if (gamePadState.DPad.Left == ButtonState.Pressed)
            {
                movement.X -= 1f;
            }

            if (gamePadState.DPad.Right == ButtonState.Pressed)
            {
                movement.X += 1f;
            }

            if (gamePadState.DPad.Up == ButtonState.Pressed)
            {
                movement.Y -= 1f;
            }

            if (gamePadState.DPad.Down == ButtonState.Pressed)
            {
                movement.Y += 1f;
            }

            return movement;
        }

        private void InitializeInputRouting()
        {
            _desktop.UseRawNavigationInput = false;
            _inputRouter = new InputRouter();
            _inputRouter.RegisterContext(new MGUIInputContext(_desktop, 100));
            _inputRouter.RegisterContext(new MiniGameHudInputContext(TryHandleHudAction, 50, IsHudContextActive));
            _inputRouter.RegisterContext(new GameplayInputContext("MiniGame.Gameplay", TryHandleGameplayAction, 0));
        }

        private bool IsHudContextActive()
            => !_isHelpOpen && !_isInventoryOpen && !_isVendorDialogOpen && !_isShopOpen;

        private void DispatchRoutedInputActions(TimeSpan totalElapsed)
        {
            foreach (KeyValuePair<Keys, BaseKeyPressedEventArgs> keyEntry in _mguiRenderer.Input.Keyboard.CurrentKeyPressedEvents)
            {
                if (keyEntry.Value == null)
                {
                    continue;
                }

                if (TryMapMiniGameUIKeyboardAction(keyEntry.Key, _mguiRenderer.Input.Keyboard.IsShiftDown, out InputAction uiAction))
                {
                    RouteGameplayAction(uiAction, new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, totalElapsed, false, keyEntry.Key));
                }

                if (TryMapMiniGameKeyboardAction(keyEntry.Key, out InputAction gameplayAction))
                {
                    RouteGameplayAction(gameplayAction, new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, totalElapsed, false, keyEntry.Key));
                }
            }

            foreach (GamePadButton button in GamePadTracker.AllButtons)
            {
                if (!_mguiRenderer.Input.GamePad.WasTriggered(button))
                {
                    continue;
                }

                if (TryMapMiniGameUIGamePadAction(button, out InputAction uiAction))
                {
                    RouteGameplayAction(uiAction, new InputActionContext(InputActionSource.GamePad, InputActionPhase.Pressed, totalElapsed, false, GamePadButton: button));
                }

                if (TryMapMiniGameGamePadAction(button, out InputAction gameplayAction))
                {
                    RouteGameplayAction(gameplayAction, new InputActionContext(InputActionSource.GamePad, InputActionPhase.Pressed, totalElapsed, false, GamePadButton: button));
                }
            }
        }

        private void RouteGameplayAction(InputAction action, InputActionContext context)
        {
            _ = _inputRouter.Route(new(action, context));
        }

        private static bool TryMapMiniGameUIKeyboardAction(Keys key, bool isShiftDown, out InputAction action)
        {
            switch (key)
            {
                case Keys.Tab:
                case Keys.Enter:
                case Keys.Space:
                case Keys.Escape:
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Apps:
                    return InputActionMapper.TryMapKeyboardAction(key, isShiftDown, out action);
                default:
                    action = default;
                    return false;
            }
        }

        private static bool TryMapMiniGameKeyboardAction(Keys key, out InputAction action)
        {
            switch (key)
            {
                case Keys.I:
                    action = InputAction.GameplaySecondary;
                    return true;
                case Keys.E:
                    action = InputAction.GameplayPrimary;
                    return true;
                case Keys.C:
                    action = InputAction.GameplayCyclePrevious;
                    return true;
                case Keys.R:
                    action = InputAction.GameplayCycleNext;
                    return true;
                case Keys.F:
                    action = InputAction.GameplayQuickUse;
                    return true;
                case Keys.H:
                    action = InputAction.GameplayToggleHelp;
                    return true;
                case Keys.P:
                    action = InputAction.Pause;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }

        private static bool TryMapMiniGameUIGamePadAction(GamePadButton button, out InputAction action)
        {
            switch (button)
            {
                case GamePadButton.A:
                case GamePadButton.B:
                case GamePadButton.Back:
                case GamePadButton.DPadUp:
                case GamePadButton.DPadDown:
                case GamePadButton.DPadLeft:
                case GamePadButton.DPadRight:
                case GamePadButton.LeftStickUp:
                case GamePadButton.LeftStickDown:
                case GamePadButton.LeftStickLeft:
                case GamePadButton.LeftStickRight:
                    return InputActionMapper.TryMapGamePadAction(button, out action);
                default:
                    action = default;
                    return false;
            }
        }

        private static bool TryMapMiniGameGamePadAction(GamePadButton button, out InputAction action)
        {
            switch (button)
            {
                case GamePadButton.Y:
                    action = InputAction.GameplaySecondary;
                    return true;
                case GamePadButton.X:
                    action = InputAction.GameplayPrimary;
                    return true;
                case GamePadButton.LeftShoulder:
                    action = InputAction.GameplayCyclePrevious;
                    return true;
                case GamePadButton.RightShoulder:
                    action = InputAction.GameplayCycleNext;
                    return true;
                case GamePadButton.RightTrigger:
                    action = InputAction.GameplayQuickUse;
                    return true;
                case GamePadButton.Back:
                    action = InputAction.GameplayToggleHelp;
                    return true;
                case GamePadButton.Start:
                    action = InputAction.Pause;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }

        private bool TryHandleHudAction(InputActionEvent actionEvent)
        {
            switch (actionEvent.Action)
            {
                case InputAction.GameplayCyclePrevious:
                    CycleQuickSlot(-1);
                    return true;
                case InputAction.GameplayCycleNext:
                    CycleQuickSlot(1);
                    return true;
                case InputAction.GameplayQuickUse:
                    return UseSelectedQuickSlot();
                default:
                    return false;
            }
        }

        private void CycleQuickSlot(int delta)
        {
            if (_quickSlots.Count == 0)
            {
                return;
            }

            _selectedQuickSlotIndex = (_selectedQuickSlotIndex + delta + _quickSlots.Count) % _quickSlots.Count;
            SetStatusMessage($"Raccourci sélectionné: {_quickSlots[_selectedQuickSlotIndex].DisplayName}.");
            UpdateHud();
        }

        private bool UseSelectedQuickSlot()
        {
            if (_quickSlots.Count == 0)
            {
                return false;
            }

            QuickSlot slot = _quickSlots[_selectedQuickSlotIndex];
            return slot.ItemName switch
            {
                "Potion" => TryUseOwnedItem(slot.ItemName, () =>
                {
                    _playerHealth = Math.Clamp(_playerHealth + 25f, 0f, CurrentMaxHealth);
                    SetStatusMessage("Potion utilisée via le HUD rapide.");
                }),
                "Potion de magie" => TryUseOwnedItem(slot.ItemName, () =>
                {
                    _playerMagic = Math.Clamp(_playerMagic + 30f, 0f, CurrentMaxMagic);
                    SetStatusMessage("Potion de magie utilisée via le HUD rapide.");
                }),
                "Antidote" => TryUseOwnedItem(slot.ItemName, () =>
                {
                    SetStatusMessage("Antidote utilisé. Vous vous sentez mieux.");
                }),
                _ => false,
            };
        }

        private bool TryUseOwnedItem(string itemName, Action onConsumed)
        {
            if (GetOwnedQuantity(itemName) <= 0)
            {
                SetStatusMessage($"Aucun {itemName.ToLowerInvariant()} disponible dans le raccourci.");
                UpdateHud();
                return true;
            }

            _ownedItems[itemName]--;
            if (_ownedItems[itemName] <= 0)
            {
                _ownedItems.Remove(itemName);
            }

            onConsumed?.Invoke();
            RefreshInventoryContent();
            UpdateHud();
            return true;
        }

        private bool TryHandleGameplayAction(InputActionEvent actionEvent)
        {
            switch (actionEvent.Action)
            {
                case InputAction.GameplayToggleHelp:
                    if (_isHelpOpen)
                    {
                        CloseHelpWindow();
                    }
                    else
                    {
                        OpenHelpWindow();
                    }
                    return true;

                case InputAction.GameplayPrimary:
                    if (_isHelpOpen || _isInventoryOpen || _isVendorDialogOpen || _isShopOpen)
                    {
                        return false;
                    }

                    if (!IsPlayerNearVendor())
                    {
                        SetStatusMessage("Le vendeur est trop loin.");
                        return true;
                    }

                    OpenVendorDialog();
                    return true;

                case InputAction.GameplaySecondary:
                    if (_isHelpOpen)
                    {
                        CloseHelpWindow();
                        return true;
                    }

                    if (_isShopOpen)
                    {
                        CloseShopWindow();
                        return true;
                    }

                    if (_isVendorDialogOpen)
                    {
                        CloseVendorDialog();
                        return true;
                    }

                    ToggleInventory();
                    return true;

                case InputAction.GameplayCyclePrevious:
                    if (_isShopOpen)
                    {
                        ToggleShopMode(-1);
                        return true;
                    }

                    return false;

                case InputAction.GameplayCycleNext:
                    if (_isShopOpen)
                    {
                        ToggleShopMode(1);
                        return true;
                    }

                    return false;

                case InputAction.Pause:
                    if (_isHelpOpen)
                    {
                        CloseHelpWindow();
                        return true;
                    }

                    if (_isShopOpen)
                    {
                        CloseShopWindow();
                        return true;
                    }

                    if (_isVendorDialogOpen)
                    {
                        CloseVendorDialog();
                        return true;
                    }

                    if (_isInventoryOpen)
                    {
                        ToggleInventory();
                        return true;
                    }

                    Exit();
                    return true;

                default:
                    return false;
            }
        }

        private bool IsGameplayMovementBlocked()
            => _isHelpOpen || _isInventoryOpen || _isVendorDialogOpen || _isShopOpen;

        private void SetStatusMessage(string text)
            => _statusText?.SetText(text ?? string.Empty, true);

        private void OpenHelpWindow()
        {
            PositionHelpWindow();
            if (!_desktop.Windows.Contains(_helpWindow))
            {
                _desktop.Windows.Add(_helpWindow);
            }

            _desktop.BringToFront(_helpWindow);
            _isHelpOpen = true;
            _helpCloseButton?.Focus(KeyboardFocusSource.Programmatic);
            SetStatusMessage("Help window opened.");
        }

        private void CloseHelpWindow()
        {
            if (_isHelpOpen)
            {
                _helpWindow.TryCloseWindow();
                _isHelpOpen = false;
                SetStatusMessage("Help window closed.");
            }
        }

        private void UseOwnedPotion(string itemName, float restoredHealth)
        {
            _ = TryUseOwnedItem(itemName, () =>
            {
                _playerHealth = Math.Clamp(_playerHealth + restoredHealth, 0f, CurrentMaxHealth);
                SetStatusMessage($"Vous utilisez {itemName} et récupérez {restoredHealth:0} PV.");
            });
        }

        private void RefreshInventorySelectionDetails()
        {
            if (string.IsNullOrEmpty(_selectedInventoryItemName) || !_itemDefinitions.TryGetValue(_selectedInventoryItemName, out ItemDefinition item))
            {
                _inventorySelectionText?.SetText("Sélectionnez un objet dans la liste.");
                _inventoryPrimaryActionButton?.SetContent("Aucune action");
                return;
            }

            int quantity = GetOwnedQuantity(item.Name);
            string actionText = item.IsConsumable
                ? $"Utiliser {item.Name}"
                : item.IsEquippable
                    ? $"Équiper ({GetEquipmentSlotDisplayName(item.EquipmentSlot)})"
                    : "Aucune action";

            _inventorySelectionText?.SetText($"{item.Description}\nQuantité possédée : {quantity}");
            _inventoryPrimaryActionButton?.SetContent(actionText);
            _inventoryUsePotionButton?.SetContent($"Boire une potion rapide x{GetOwnedQuantity("Potion")}");
        }

        private void ExecuteInventorySelectionAction()
        {
            if (string.IsNullOrEmpty(_selectedInventoryItemName) || !_itemDefinitions.TryGetValue(_selectedInventoryItemName, out ItemDefinition item))
            {
                return;
            }

            if (item.IsConsumable)
            {
                _ = TryUseOwnedItem(item.Name, () =>
                {
                    if (item.HealthRestore > 0f)
                    {
                        _playerHealth = Math.Clamp(_playerHealth + item.HealthRestore, 0f, CurrentMaxHealth);
                    }

                    if (item.MagicRestore > 0f)
                    {
                        _playerMagic = Math.Clamp(_playerMagic + item.MagicRestore, 0f, CurrentMaxMagic);
                    }

                    SetStatusMessage($"{item.Name} utilisé depuis l'inventaire.");
                });
                return;
            }

            if (item.IsEquippable)
            {
                EquipItem(item);
            }
        }

        private void EquipItem(ItemDefinition item)
        {
            if (!item.IsEquippable || GetOwnedQuantity(item.Name) <= 0)
            {
                SetStatusMessage($"Impossible d'équiper {item.Name}.");
                return;
            }

            if (_equippedItems.TryGetValue(item.EquipmentSlot, out string currentItem) && currentItem == item.Name)
            {
                SetStatusMessage($"{item.Name} est déjà équipé.");
                return;
            }

            AddOwnedItem(currentItem, 1);
            RemoveOwnedItem(item.Name, 1);
            _equippedItems[item.EquipmentSlot] = item.Name;

            RecalculateDerivedStats();
            SetStatusMessage($"{item.Name} équipé sur {GetEquipmentSlotDisplayName(item.EquipmentSlot)}.");
            RefreshInventoryContent();
            UpdateHud();
        }

        private void UnequipItem(EquipmentSlot slot)
        {
            if (!_equippedItems.TryGetValue(slot, out string itemName) || string.IsNullOrEmpty(itemName))
            {
                return;
            }

            AddOwnedItem(itemName, 1);
            _equippedItems[slot] = string.Empty;
            RecalculateDerivedStats();
            SetStatusMessage($"{itemName} retiré de {GetEquipmentSlotDisplayName(slot)}.");
            RefreshInventoryContent();
            UpdateHud();
        }

        private bool IsPlayerNearVendor()
        {
            if (_player == null || _vendor == null)
            {
                return false;
            }

            return Vector2.Distance(_player.Position, _vendor.Position) <= VendorInteractionRadius;
        }

        private void ToggleInventory()
        {
            if (_isInventoryOpen)
            {
                _inventoryWindow.TryCloseWindow();
                _isInventoryOpen = false;
                SetStatusMessage("Inventaire fermé.");
                return;
            }

            RefreshInventoryContent();
            PositionInventoryWindow();
            if (!_desktop.Windows.Contains(_inventoryWindow))
            {
                _desktop.Windows.Add(_inventoryWindow);
            }

            _desktop.BringToFront(_inventoryWindow);
            _isInventoryOpen = true;
            _inventoryOwnedItemsList?.Focus(KeyboardFocusSource.Programmatic);
            SetStatusMessage("Inventaire ouvert.");
        }

        private void OpenVendorDialog()
        {
            if (_vendorDialogWindow == null)
            {
                return;
            }

            PositionVendorDialogWindow();
            if (!_desktop.Windows.Contains(_vendorDialogWindow))
            {
                _desktop.Windows.Add(_vendorDialogWindow);
            }

            _desktop.BringToFront(_vendorDialogWindow);
            _isVendorDialogOpen = true;
            _vendorDialogText.SetText("Bonjour voyageur. J'ai quelques provisions et une cape qui résiste bien à la pluie.");
            _vendorBuyButton?.Focus(KeyboardFocusSource.Programmatic);
            SetStatusMessage("Conversation avec le vendeur.");
        }

        private void CloseVendorDialog()
        {
            if (_isVendorDialogOpen)
            {
                _vendorDialogWindow.TryCloseWindow();
                _isVendorDialogOpen = false;
                SetStatusMessage("Vous quittez le vendeur.");
            }
        }

        private void OpenShopWindow()
        {
            if (_shopWindow == null)
            {
                return;
            }

            CloseVendorDialog();

            PositionShopWindow();
            if (!_desktop.Windows.Contains(_shopWindow))
            {
                _desktop.Windows.Add(_shopWindow);
            }

            _desktop.BringToFront(_shopWindow);
            _isShopOpen = true;
            _shopMode = TradeMode.Buy;
            _shopStatusText.SetText("Bienvenue, voyageur.");
            SetStatusMessage("La boutique est ouverte.");

            RefreshShopContent();
            RefreshShopSelectionDetails();
            _shopOffersList?.Focus(KeyboardFocusSource.Programmatic);
        }

        private void CloseShopWindow()
        {
            if (_isShopOpen)
            {
                _shopWindow.TryCloseWindow();
                _isShopOpen = false;
                SetStatusMessage("Vous quittez la boutique.");
            }
        }

        private void ExecuteSelectedTrade()
        {
            TradeEntry offer = _selectedTradeEntry;
            if (offer == null)
            {
                _shopStatusText.SetText(_shopMode == TradeMode.Buy ? "Sélectionnez un objet à acheter." : "Sélectionnez un objet à vendre.");
                SetStatusMessage("Aucun objet sélectionné.");
                return;
            }

            if (_shopMode == TradeMode.Buy)
            {
                if (_playerMoney < offer.Price)
                {
                    _shopStatusText.SetText($"Pas assez d'or pour acheter {offer.Item.Name}.");
                    SetStatusMessage($"Vous n'avez pas assez d'or pour {offer.Item.Name}.");
                    return;
                }

                _playerMoney -= offer.Price;
                AddOwnedItem(offer.Item.Name, 1);
                _shopStatusText.SetText($"{offer.Item.Name} ajouté à l'inventaire.");
                SetStatusMessage($"Achat réussi: {offer.Item.Name}.");
            }
            else
            {
                if (GetOwnedQuantity(offer.Item.Name) <= 0)
                {
                    _shopStatusText.SetText($"Vous n'avez plus de {offer.Item.Name} à vendre.");
                    SetStatusMessage($"Impossible de vendre {offer.Item.Name}.");
                    RefreshShopContent();
                    RefreshShopSelectionDetails();
                    return;
                }

                RemoveOwnedItem(offer.Item.Name, 1);
                _playerMoney += offer.Price;
                _shopStatusText.SetText($"{offer.Item.Name} vendu au marchand.");
                SetStatusMessage($"Vente réussie: {offer.Item.Name}.");
            }

            RefreshInventoryContent();
            RefreshShopContent();
            RefreshShopSelectionDetails();
            UpdateHud();
        }

        private void ToggleShopMode(int direction)
        {
            _shopMode = direction >= 0
                ? (_shopMode == TradeMode.Buy ? TradeMode.Sell : TradeMode.Buy)
                : (_shopMode == TradeMode.Sell ? TradeMode.Buy : TradeMode.Sell);

            SetStatusMessage(_shopMode == TradeMode.Buy ? "Mode achat activé." : "Mode vente activé.");
            RefreshShopContent();
            RefreshShopSelectionDetails();
        }

        private void RefreshTradeEntries()
        {
            string previousSelectionName = _selectedTradeEntry?.Item.Name;
            List<TradeEntry> entries = _shopMode == TradeMode.Buy
                ? _vendorOfferItemNames.Where(_itemDefinitions.ContainsKey)
                    .Select(name => new TradeEntry(_itemDefinitions[name], _itemDefinitions[name].BuyPrice, 1, true))
                    .ToList()
                : _ownedItems
                    .Where(x => x.Value > 0 && _itemDefinitions.TryGetValue(x.Key, out ItemDefinition definition) && definition.CanSell)
                    .Select(x => new TradeEntry(_itemDefinitions[x.Key], _itemDefinitions[x.Key].SellPrice, x.Value, false))
                    .OrderBy(x => x.Item.Name)
                    .ToList();

            _shopOffersList?.SetItemsSource(entries);
            _selectedTradeEntry = entries.FirstOrDefault(x => x.Item.Name == previousSelectionName) ?? entries.FirstOrDefault();
            if (_selectedTradeEntry != null)
            {
                _shopOffersList.SelectedValue = _selectedTradeEntry;
            }
        }

        private int GetOwnedQuantity(string itemName)
            => _ownedItems.TryGetValue(itemName, out int quantity) ? quantity : 0;

        private void AddOwnedItem(string itemName, int quantity)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0)
            {
                return;
            }

            if (_ownedItems.ContainsKey(itemName))
            {
                _ownedItems[itemName] += quantity;
            }
            else
            {
                _ownedItems[itemName] = quantity;
            }
        }

        private void RemoveOwnedItem(string itemName, int quantity)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0 || !_ownedItems.ContainsKey(itemName))
            {
                return;
            }

            _ownedItems[itemName] -= quantity;
            if (_ownedItems[itemName] <= 0)
            {
                _ownedItems.Remove(itemName);
            }
        }

        private float CurrentMaxHealth => BaseMaxHealth + GetEquippedBonus(x => x.BonusHealth);
        private float CurrentMaxMagic => BaseMaxMagic + GetEquippedBonus(x => x.BonusMagic);

        private void RecalculateDerivedStats()
        {
            if (_player != null)
            {
                _player.MovementSpeed = BaseMoveSpeed + GetEquippedBonus(x => x.BonusMoveSpeed);
            }

            _playerHealth = Math.Clamp(_playerHealth, 0f, CurrentMaxHealth);
            _playerMagic = Math.Clamp(_playerMagic, 0f, CurrentMaxMagic);
        }

        private float GetEquippedBonus(Func<ItemDefinition, float> selector)
        {
            float total = 0f;
            foreach (string equippedName in _equippedItems.Values)
            {
                if (!string.IsNullOrEmpty(equippedName) && _itemDefinitions.TryGetValue(equippedName, out ItemDefinition definition))
                {
                    total += selector(definition);
                }
            }

            return total;
        }

        private static string GetEquipmentSlotDisplayName(EquipmentSlot slot)
            => slot switch
            {
                EquipmentSlot.Weapon => "Arme",
                EquipmentSlot.Armor => "Armure",
                EquipmentSlot.Accessory => "Accessoire",
                _ => "Inconnu",
            };

        private void PositionInventoryWindow()
        {
            _inventoryWindow.Left = (GraphicsDevice.Viewport.Width - _inventoryWindow.WindowWidth) / 2;
            _inventoryWindow.Top = (GraphicsDevice.Viewport.Height - _inventoryWindow.WindowHeight) / 2;
            _inventoryWindow.ValidateWindowSizeAndPosition();
        }

        private void PositionHelpWindow()
        {
            if (_helpWindow == null)
            {
                return;
            }

            _helpWindow.Left = (GraphicsDevice.Viewport.Width - _helpWindow.WindowWidth) / 2;
            _helpWindow.Top = (GraphicsDevice.Viewport.Height - _helpWindow.WindowHeight) / 2 - 20;
            _helpWindow.ValidateWindowSizeAndPosition();
        }

        private void PositionVendorDialogWindow()
        {
            if (_vendorDialogWindow == null)
            {
                return;
            }

            _vendorDialogWindow.Left = (GraphicsDevice.Viewport.Width - _vendorDialogWindow.WindowWidth) / 2 - 180;
            _vendorDialogWindow.Top = (GraphicsDevice.Viewport.Height - _vendorDialogWindow.WindowHeight) / 2 - 40;
            _vendorDialogWindow.ValidateWindowSizeAndPosition();
        }

        private void PositionShopWindow()
        {
            if (_shopWindow == null)
            {
                return;
            }

            int left = (GraphicsDevice.Viewport.Width - _shopWindow.WindowWidth) / 2 + 220;
            int maxLeft = Math.Max(16, GraphicsDevice.Viewport.Width - _shopWindow.WindowWidth - 16);

            _shopWindow.Left = Math.Clamp(left, 16, maxLeft);
            _shopWindow.Top = (GraphicsDevice.Viewport.Height - _shopWindow.WindowHeight) / 2;
            _shopWindow.ValidateWindowSizeAndPosition();
        }

        private void HandleClientSizeChanged()
        {
            if (_player != null)
            {
                ClampEntityToViewport(_player);
            }

            if (_vendor != null)
            {
                ClampEntityToViewport(_vendor);
            }

            if (_inventoryWindow != null)
            {
                PositionInventoryWindow();
            }

            if (_helpWindow != null)
            {
                PositionHelpWindow();
            }

            if (_vendorDialogWindow != null)
            {
                PositionVendorDialogWindow();
            }

            if (_shopWindow != null)
            {
                PositionShopWindow();
            }
        }

        private void ClampEntityToViewport(CircleEntity entity)
        {
            Viewport viewport = GraphicsDevice.Viewport;
            entity.Position = new Vector2(
                MathHelper.Clamp(entity.Position.X, entity.Radius, viewport.Width - entity.Radius),
                MathHelper.Clamp(entity.Position.Y, entity.Radius, viewport.Height - entity.Radius));
        }

        private void DrawEntity(CircleEntity entity)
        {
            int diameter = (int)MathF.Round(entity.Radius * 2f);
            Rectangle destination = new(
                (int)MathF.Round(entity.Position.X - entity.Radius),
                (int)MathF.Round(entity.Position.Y - entity.Radius),
                diameter,
                diameter);

            _spriteBatch.Draw(_circleTexture, destination, entity.Color);
        }

        private Texture2D CreateCircleTexture(int diameter)
        {
            Texture2D texture = new(GraphicsDevice, diameter, diameter);
            Color[] data = new Color[diameter * diameter];

            float radius = diameter / 2f;
            Vector2 center = new((diameter - 1) / 2f, (diameter - 1) / 2f);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = MathHelper.Clamp(radius - distance, 0f, 1f);
                    data[y * diameter + x] = Color.White * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }

        private static Dictionary<string, ItemDefinition> CreateItemDefinitions()
        {
            return new(StringComparer.OrdinalIgnoreCase)
            {
                ["Potion"] = new ItemDefinition("Potion", "Restores 25 health.", 12, 6, healthRestore: 25f),
                ["Potion de magie"] = new ItemDefinition("Potion de magie", "Restores 30 magic.", 18, 9, magicRestore: 30f),
                ["Antidote"] = new ItemDefinition("Antidote", "Cures minor afflictions. Useful for the quick HUD demo.", 10, 5),
                ["Élixir"] = new ItemDefinition("Élixir", "Restores 20 health and 20 magic.", 35, 18, healthRestore: 20f, magicRestore: 20f),
                ["Cape du voyageur"] = new ItemDefinition("Cape du voyageur", "A light armor piece that boosts max health and movement speed.", 60, 30, EquipmentSlot.Armor, bonusHealth: 10f, bonusMoveSpeed: 18f),
                ["Dague du marchand"] = new ItemDefinition("Dague du marchand", "A nimble weapon with a light movement bonus.", 45, 22, EquipmentSlot.Weapon, bonusMoveSpeed: 22f),
                ["Anneau du troc"] = new ItemDefinition("Anneau du troc", "A bright accessory that boosts magic reserves.", 80, 40, EquipmentSlot.Accessory, bonusMagic: 25f),
                ["Épée en fer"] = new ItemDefinition("Épée en fer", "Your reliable starting sword.", 0, 18, EquipmentSlot.Weapon, canSell: true),
                ["Tunique de cuir"] = new ItemDefinition("Tunique de cuir", "Your starting armor. Durable and familiar.", 0, 16, EquipmentSlot.Armor, bonusHealth: 6f, canSell: true),
                ["Anneau de mana"] = new ItemDefinition("Anneau de mana", "A starting accessory with a small mana bonus.", 0, 20, EquipmentSlot.Accessory, bonusMagic: 12f, canSell: true),
            };
        }
    }
}
