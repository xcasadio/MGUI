// ============================================================================
// EXEMPLE D'UTILISATION - Save/Load Layout
// ============================================================================
// Ce fichier montre comment sauvegarder et charger un layout de docking.
// ============================================================================

using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using System;
using System.Collections.Generic;
using System.IO;

namespace MGUI.Samples.DockingExamples
{
    public class SaveLoadLayoutExample
    {
        private MGDockHost _dockHost;

        // ========================================================================
        // SAVE LAYOUT
        // ========================================================================
        public void SaveLayoutExample()
        {
            // Méthode 1 : Via extension method (recommandé)
            string json = _dockHost.SaveLayoutToJson(indented: true);
            
            // Sauvegarder dans un fichier
            File.WriteAllText("my_layout.json", json);
            
            Console.WriteLine("Layout sauvegardé !");
        }

        // ========================================================================
        // LOAD LAYOUT
        // ========================================================================
        public void LoadLayoutExample()
        {
            // Charger le JSON depuis un fichier
            string json = File.ReadAllText("my_layout.json");

            // Méthode 1 : Load simple (sans reconnexion de contenu)
            // Les panels seront créés mais avec ContentFactory = null
            _dockHost.LoadLayoutFromJson(json);

            // Méthode 2 : Load avec factory pour reconnecter le contenu
            _dockHost.LoadLayoutFromJson(json, panelFactory: panelId =>
            {
                // Cette fonction est appelée pour chaque panel du layout
                // Elle doit retourner une Func<MGElement> qui crée le contenu du panel
                
                return panelId switch
                {
                    "properties_panel" => () => CreatePropertiesPanel(),
                    "output_panel" => () => CreateOutputPanel(),
                    "explorer_panel" => () => CreateExplorerPanel(),
                    _ => null // Panel inconnu - pas de contenu
                };
            });

            Console.WriteLine("Layout chargé !");
        }

        // ========================================================================
        // LOAD avec REGISTRY (pattern recommandé)
        // ========================================================================
        // Si vous avez un registre de panels, vous pouvez l'utiliser pour
        // reconnecter automatiquement les contenus.
        // ========================================================================
        
        private Dictionary<string, Func<MGElement>> _panelFactoryRegistry = new();

        public void RegisterPanels()
        {
            // Enregistrer les factories de panels au démarrage de l'application
            _panelFactoryRegistry["properties_panel"] = () => CreatePropertiesPanel();
            _panelFactoryRegistry["output_panel"] = () => CreateOutputPanel();
            _panelFactoryRegistry["explorer_panel"] = () => CreateExplorerPanel();
        }

        public void LoadLayoutWithRegistry()
        {
            string json = File.ReadAllText("my_layout.json");

            _dockHost.LoadLayoutFromJson(json, panelFactory: panelId =>
            {
                // Chercher dans le registre
                if (_panelFactoryRegistry.TryGetValue(panelId, out var factory))
                {
                    return factory;
                }

                Console.WriteLine($"Warning: Panel '{panelId}' not found in registry.");
                return null;
            });
        }

        // ========================================================================
        // SAVE/LOAD avec VÉRIFICATION
        // ========================================================================
        public bool TrySaveLayout(string filePath)
        {
            try
            {
                if (_dockHost?.LayoutModel == null)
                {
                    Console.WriteLine("Error: No layout to save.");
                    return false;
                }

                string json = _dockHost.SaveLayoutToJson(indented: true);
                File.WriteAllText(filePath, json);
                
                Console.WriteLine($"Layout saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving layout: {ex.Message}");
                return false;
            }
        }

        public bool TryLoadLayout(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Error: File not found: {filePath}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                _dockHost.LoadLayoutFromJson(json, panelFactory: GetPanelFactory);
                
                Console.WriteLine($"Layout loaded from: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading layout: {ex.Message}");
                return false;
            }
        }

        private Func<MGElement> GetPanelFactory(string panelId)
        {
            // Votre logique de factory ici
            return _panelFactoryRegistry.TryGetValue(panelId, out var factory) ? factory : null;
        }

        // ========================================================================
        // EXEMPLE DE FORMAT JSON GÉNÉRÉ
        // ========================================================================
        /*
        {
          "version": "1.0",
          "rootNode": {
            "type": "Split",
            "id": "split-1",
            "orientation": "Horizontal",
            "splitRatio": 0.7,
            "minFirstSize": 100,
            "minSecondSize": 100,
            "firstChild": {
              "type": "TabGroup",
              "id": "group-1",
              "activePanelId": "panel-1",
              "panels": [
                {
                  "id": "panel-1",
                  "title": "Properties",
                  "icon": "icon_properties",
                  "canClose": true,
                  "canFloat": true,
                  "isPinned": true
                }
              ]
            },
            "secondChild": {
              "type": "TabGroup",
              "id": "group-2",
              "activePanelId": "panel-2",
              "panels": [
                {
                  "id": "panel-2",
                  "title": "Output",
                  "icon": "icon_output",
                  "canClose": false,
                  "canFloat": true,
                  "isPinned": true
                }
              ]
            }
          }
        }
        */

        // ========================================================================
        // Dummy methods pour l'exemple
        // ========================================================================
        private MGElement CreatePropertiesPanel() => null;
        private MGElement CreateOutputPanel() => null;
        private MGElement CreateExplorerPanel() => null;
    }
}
