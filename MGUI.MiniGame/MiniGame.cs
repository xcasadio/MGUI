using System;
using System.Collections.Generic;
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
        private MGWindow _inventoryWindow;
        private MGWindow _vendorDialogWindow;
        private MGWindow _shopWindow;
        private MGProgressBar _healthBar;
        private MGProgressBar _magicBar;
        private MGTextBlock _moneyText;
        private MGTextBlock _interactionText;
        private MGTextBlock _statusText;
        private MGTextBlock _vendorDialogText;
        private MGTextBlock _shopMoneyText;
        private MGTextBlock _shopOwnedQuantityText;
        private MGTextBlock _shopSelectedItemText;
        private MGTextBlock _shopStatusText;
        private MGStackPanel _inventoryContent;
        private MGStackPanel _shopContent;
        private MGListBox<ShopOffer> _shopOffersList;
        private MGButton _inventoryUsePotionButton;
        private MGButton _inventoryCloseButton;
        private MGButton _vendorBuyButton;
        private MGButton _vendorLeaveButton;
        private MGButton _shopBuyButton;
        private MGButton _shopLeaveButton;
        private ShopOffer _selectedShopOffer;

        private readonly List<CircleEntity> _entities = [];
        private CircleEntity _player;
        private CircleEntity _vendor;

        private bool _isInventoryOpen;
        private bool _isVendorDialogOpen;
        private bool _isShopOpen;

        private const float MaxHealth = 100f;
        private const float MaxMagic = 100f;
        private float _playerHealth = 85f;
        private float _playerMagic = 60f;
        private int _playerMoney = 125;

        private readonly Dictionary<string, string> _equippedItems = new()
        {
            ["Arme"] = "Épée en fer",
            ["Armure"] = "Tunique de cuir",
            ["Accessoire"] = "Anneau de mana"
        };

        private readonly Dictionary<string, int> _ownedItems = new()
        {
            ["Potion"] = 5,
            ["Potion de magie"] = 3,
            ["Herbe médicinale"] = 4,
            ["Antidote"] = 2,
            ["Clé du marchand"] = 1
        };

        private readonly List<ShopOffer> _vendorOffers =
        [
            new("Potion", 12),
            new("Potion de magie", 18),
            new("Antidote", 10),
            new("Élixir", 35),
            new("Cape du voyageur", 60)
        ];

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
            CreateInventoryWindow();
            CreateVendorDialogWindow();
            CreateShopWindow();
            InitializeInputRouting();
            InitializeEntities();

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

            DispatchRoutedGameplayActions(gameTime.TotalGameTime);

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

            _hudWindow.SetContent(content);
            _desktop.Windows.Add(_hudWindow);
            UpdateHud();
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
            _shopContent.TryAddChild(new MGTextBlock(_shopWindow, "Objets vendus", Color.White, 18));

            MGDockPanel body = new(_shopWindow)
            {
                PreferredWidth = 410,
                PreferredHeight = 240,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _shopOffersList = new MGListBox<ShopOffer>(_shopWindow)
            {
                PreferredWidth = 240,
                PreferredHeight = 220,
                SelectionMode = ListBoxSelectionMode.Single,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Header = new MGTextBlock(_shopWindow, "Nom / Prix", Color.White, 14)
            };
            _shopOffersList.ItemTemplate = CreateShopOfferListItem;
            _shopOffersList.SetItemsSource(_vendorOffers);
            _shopOffersList.SelectionChanged += (_, _) =>
            {
                _selectedShopOffer = _shopOffersList.SelectedValue;
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

            _shopOwnedQuantityText = new MGTextBlock(_shopWindow, string.Empty, Color.White, 14);
            rightPanel.TryAddChild(_shopOwnedQuantityText);

            _shopSelectedItemText = new MGTextBlock(_shopWindow, string.Empty, Color.LightGray, 14);
            rightPanel.TryAddChild(_shopSelectedItemText);

            _shopBuyButton = new MGButton(_shopWindow, _ => BuySelectedShopItem())
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
            if (_vendorOffers.Count > 0)
            {
                _shopOffersList.SelectedValue = _vendorOffers[0];
                _selectedShopOffer = _vendorOffers[0];
            }

            RefreshShopContent();
            RefreshShopSelectionDetails();
            PositionShopWindow();
        }

        private MGElement CreateShopOfferListItem(ShopOffer offer)
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

            MGTextBlock nameText = new(_shopWindow, offer.Name, Color.White, 14)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            row.TryAddChild(priceText, Dock.Right);
            row.TryAddChild(nameText, Dock.Left);
            return row;
        }

        private void RefreshInventoryContent()
        {
            _inventoryContent.TryRemoveAll();

            _inventoryContent.TryAddChild(new MGTextBlock(_inventoryWindow, "Équipement courant", Color.White, 18));

            foreach (var equippedItem in _equippedItems)
            {
                _inventoryContent.TryAddChild(new MGTextBlock(_inventoryWindow, $"- {equippedItem.Key} : {equippedItem.Value}", Color.LightGray, 14));
            }

            _inventoryContent.TryAddChild(new MGTextBlock(_inventoryWindow, "Objets possédés", Color.White, 18)
            {
                Margin = new Thickness(0, 8, 0, 0)
            });

            foreach (var ownedItem in _ownedItems)
            {
                _inventoryContent.TryAddChild(new MGTextBlock(_inventoryWindow, $"- {ownedItem.Key} x{ownedItem.Value}", Color.Gold, 14));
            }

            MGStackPanel actions = new(_inventoryWindow, Orientation.Vertical)
            {
                Spacing = 6,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _inventoryUsePotionButton = new MGButton(_inventoryWindow, _ => UseOwnedPotion("Potion", 25f))
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _inventoryUsePotionButton.SetContent($"Boire une potion (+25 PV) x{GetOwnedQuantity("Potion")}");
            actions.TryAddChild(_inventoryUsePotionButton);

            _inventoryCloseButton = new MGButton(_inventoryWindow, _ => ToggleInventory())
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _inventoryCloseButton.SetContent("Fermer l'inventaire");
            actions.TryAddChild(_inventoryCloseButton);

            _inventoryContent.TryAddChild(actions);
            _inventoryWindow.DefaultFocusElement = _inventoryUsePotionButton;
        }

        private void RefreshShopContent()
        {
            _shopMoneyText?.SetText($"Votre argent : {_playerMoney} or");
        }

        private void RefreshShopSelectionDetails()
        {
            if (_selectedShopOffer == null)
            {
                _shopOwnedQuantityText?.SetText("Quantité : 0");
                _shopSelectedItemText?.SetText("Aucun objet sélectionné.");
                _shopBuyButton?.SetContent("Acheter");
                return;
            }

            int quantityOwned = GetOwnedQuantity(_selectedShopOffer.Name);
            _shopOwnedQuantityText?.SetText($"Quantité : {quantityOwned}");
            _shopSelectedItemText?.SetText($"Objet : {_selectedShopOffer.Name}");
            _shopBuyButton?.SetContent($"Acheter ({_selectedShopOffer.Price} or)");
        }

        private void UpdateHud()
        {
            _healthBar.Maximum = MaxHealth;
            _healthBar.Value = Math.Clamp(_playerHealth, 0f, MaxHealth);

            _magicBar.Maximum = MaxMagic;
            _magicBar.Value = Math.Clamp(_playerMagic, 0f, MaxMagic);

            _moneyText.SetText($"Argent : {_playerMoney}");
            _interactionText.SetText(GetInteractionPrompt(), true);
            RefreshShopContent();
        }

        private string GetInteractionPrompt()
        {
            if (_isShopOpen)
            {
                return "Boutique ouverte: Tab/Fleches/Entree pour naviguer, I ou Y pour fermer.";
            }

            if (_isVendorDialogOpen)
            {
                return "Dialogue vendeur: Tab/Fleches/Entree pour choisir, I ou Y pour fermer.";
            }

            if (_isInventoryOpen)
            {
                return "Inventaire ouvert: utilisez les boutons ou I/Y pour fermer.";
            }

            if (IsPlayerNearVendor())
            {
                return "Pres du vendeur: E ou X pour parler, I ou Y pour l'inventaire.";
            }

            return "Deplacement: WASD/Fleches ou stick. Inventaire: I/Y.";
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
            _inputRouter = new InputRouter();
            _inputRouter.RegisterContext(new MGUIInputContext(_desktop, 100));
            _inputRouter.RegisterContext(new GameplayInputContext("MiniGame.Gameplay", TryHandleGameplayAction, 0));
        }

        private void DispatchRoutedGameplayActions(TimeSpan totalElapsed)
        {
            foreach (KeyValuePair<Keys, BaseKeyPressedEventArgs> keyEntry in _mguiRenderer.Input.Keyboard.CurrentKeyPressedEvents)
            {
                if (keyEntry.Value == null || !TryMapMiniGameKeyboardAction(keyEntry.Key, out InputAction action))
                {
                    continue;
                }

                RouteGameplayAction(action, new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, totalElapsed, false, keyEntry.Key));
            }

            foreach (GamePadButton button in GamePadTracker.AllButtons)
            {
                if (!_mguiRenderer.Input.GamePad.WasTriggered(button) || !TryMapMiniGameGamePadAction(button, out InputAction action))
                {
                    continue;
                }

                RouteGameplayAction(action, new InputActionContext(InputActionSource.GamePad, InputActionPhase.Pressed, totalElapsed, false, GamePadButton: button));
            }
        }

        private void RouteGameplayAction(InputAction action, InputActionContext context)
        {
            _ = _inputRouter.Route(new(action, context));
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
                case Keys.P:
                    action = InputAction.Pause;
                    return true;
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
                case GamePadButton.Start:
                    action = InputAction.Pause;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }

        private bool TryHandleGameplayAction(InputActionEvent actionEvent)
        {
            switch (actionEvent.Action)
            {
                case InputAction.GameplayPrimary:
                    if (_isInventoryOpen || _isVendorDialogOpen || _isShopOpen)
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

                case InputAction.Pause:
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
            => _isInventoryOpen || _isVendorDialogOpen || _isShopOpen;

        private void SetStatusMessage(string text)
            => _statusText?.SetText(text ?? string.Empty, true);

        private void UseOwnedPotion(string itemName, float restoredHealth)
        {
            if (GetOwnedQuantity(itemName) <= 0)
            {
                SetStatusMessage($"Vous n'avez plus de {itemName.ToLowerInvariant()}.");
                RefreshInventoryContent();
                return;
            }

            _ownedItems[itemName]--;
            if (_ownedItems[itemName] <= 0)
            {
                _ownedItems.Remove(itemName);
            }

            _playerHealth = Math.Clamp(_playerHealth + restoredHealth, 0f, MaxHealth);
            SetStatusMessage($"Vous utilisez {itemName} et récupérez {restoredHealth:0} PV.");
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
            _inventoryUsePotionButton?.Focus(KeyboardFocusSource.Programmatic);
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
            _shopStatusText.SetText("Bienvenue, voyageur.");
            SetStatusMessage("La boutique est ouverte.");

            if (_selectedShopOffer == null && _vendorOffers.Count > 0)
            {
                _selectedShopOffer = _vendorOffers[0];
                _shopOffersList.SelectedValue = _selectedShopOffer;
            }

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

        private void BuySelectedShopItem()
        {
            ShopOffer offer = _selectedShopOffer;
            if (offer == null)
            {
                _shopStatusText.SetText("Sélectionnez un objet à acheter.");
                SetStatusMessage("Aucun objet sélectionné.");
                return;
            }

            if (_playerMoney < offer.Price)
            {
                _shopStatusText.SetText($"Pas assez d'or pour acheter {offer.Name}.");
                SetStatusMessage($"Vous n'avez pas assez d'or pour {offer.Name}.");
                return;
            }

            _playerMoney -= offer.Price;

            if (_ownedItems.ContainsKey(offer.Name))
            {
                _ownedItems[offer.Name]++;
            }
            else
            {
                _ownedItems[offer.Name] = 1;
            }

            _shopStatusText.SetText($"{offer.Name} ajouté à l'inventaire.");
            SetStatusMessage($"Achat réussi: {offer.Name}.");
            RefreshInventoryContent();
            RefreshShopContent();
            RefreshShopSelectionDetails();
            UpdateHud();
        }

        private int GetOwnedQuantity(string itemName)
            => _ownedItems.TryGetValue(itemName, out int quantity) ? quantity : 0;

        private void PositionInventoryWindow()
        {
            _inventoryWindow.Left = (GraphicsDevice.Viewport.Width - _inventoryWindow.WindowWidth) / 2;
            _inventoryWindow.Top = (GraphicsDevice.Viewport.Height - _inventoryWindow.WindowHeight) / 2;
            _inventoryWindow.ValidateWindowSizeAndPosition();
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
    }
}
