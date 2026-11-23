# Required Unity Asset Store packages

`Assets/Scenes/Castle_Exterior.unity` uses two free Unity Asset Store packages. Their raw source files are intentionally excluded from this public repository because each contributor must acquire the package under their own Unity Asset Store license.

## Install before opening the scene

1. Use Unity `2022.3.62f3c1` and open **Window → Package Manager → My Assets**.
2. Add and import [European Forests - Realistic Trees](https://assetstore.unity.com/packages/3d/vegetation/trees/european-forests-realistic-trees-229716), version `1.0`.
3. Add and import [RPG Medieval Props DEMO](https://assetstore.unity.com/packages/3d/props/rpg-medieval-props-demo-248681), version `1.1.3`.
4. In Unity's Project window, move the imported package folders to these exact paths:
   - `Assets/Moudles/Realistic Tree`
   - `Assets/Moudles/RPG MEDIEVAL PROPS`
5. Open `Assets/Scenes/Castle_Exterior.unity` and allow Unity to finish importing.

The scene references package assets by their Unity GUIDs. Importing the listed versions restores the tree and prop references; the exact folder paths are also required by the environment rebuilding tools under **Tools → Castle Environment**.
