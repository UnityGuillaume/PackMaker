# Pack Maker

This tool was made to simplify creating multiple "packs" from the same Unity
project.
E.g. you want to export a "Vegetation" pack from all your Vegetation models and
material, a "Character" pack from all the characters etc.

Instead of having to copy past things around manually, or keep 10 different
branches/repositories, you just create 10 "packs" asset and add to them the
files you want them to contains. You can even recreate a completely different
folder structure.

Then with one button click, you can re-org the project to that pack.

_Note : this will move the assets around, not copy them, because otherwise
references would be lost and patching them is tricky._

## Usage

Open the Pack Maker throught the menu **Content Team Tools/Pack Maker**

In the top dropdown, select *New...* to create a new pack.

Then drag and drop your asset in the window to add them :

- if you drop an asset it will be added where you dropped it (so root if dropped
  in an empty place, or inside a folder if dropped on a folder)
- if you drop a folder, all of its content (folder and files) will be added
where your dropped it.

You can re-arange the resulting hierarchy by **drag and dropping things around**

You can **create a new folder** by right clicking an empty space to add it at
the root, or right clicking a folder to add it as child of that folder.

Click on *Build this pack* to  build the currently edited pack

### Cleaning

Click on *Revert Pack* to move the file back at their place once that done and
clean the project.

**I highly recommend to have a source control setup on the project though, this
is still a WIP tools and could mess up file location or loose file on revert**

## Possible improvement & TODOs

- Auto Cleaning
- Check if files have all their dependency in the pack.
