# SPARK: Simple Profile and Roleplay Kit

SPARK is a [Blish HUD](https://blishhud.com) module for Guild Wars 2 roleplayers. It lets players create character profiles, share their current roleplay status, discover other SPARK users, and save notes or bookmarks for profiles they have viewed.

Want more information or want to see screenshots of the module in action? Check out [getspark.fyi](https://getspark.fyi) for more details!

There is now a [Discord server](https://discord.gg/nJ3UstHcAg) for SPARK if you wish to report bugs, provide feedback, or need assistance while using it.

## Features

- Multiple profiles for each character
- RP Statuses (Online, Looking for RP, Busy, Invisible (appear offline))
- 'At a Glance' icons with custom tooltips including a fast, built-in icon search with over 100,000 icons (26,000 unique) from the game to use
- Group dice rolling with private rooms up to 50 users to roll dice for RP events
- Nearby players tracking: an opt-in feature to display your location to others on the same map, which map IP you are on, and how far away you are!
- Bookmarks for your favourite RP profiles
- Private notes that you can save to any profile you view
- Privacy options to hide your current location or appear offline
- Filter/sorting for profiles on the online list, recently seen, or bookmarked lists
- Block option (Account-wide) to block all profiles from someone. This prevents blocked users from seeing you online or fetching your profile anymore
- Report option to report offensive profiles or users who promote hate or bigotry.
- Mature Profile opt-in / filtering

## Installing SPARK

Please follow the instructions on the [Release Page](https://github.com/at-bat/Blish-HUD-SPARK/releases) or on the [SPARK website](https://getspark.fyi/install/)

**This process will be simplified once it has been added to Blish HUD's module repository.**

## Requirements

- [Blish HUD](https://blishhud.com) 1.3.0 or newer
- Guild Wars 2 API key (added to your Blish HUD) with account and characters permissions.

SPARK uses these permissions to verify account and character ownership and tie profiles to your GW2 account.

This is done to prevent potential impersonation or abuse issues and provide SPARK with the ability to restrict malicious users from using the module.

Guild Wars 2 API Keys provide read-only access to your account for information like account name and your characters, their race, profession, etc., and are safe to use within Blish HUD.

## Privacy / Data

To share your RP profile with other players, this module relays your data through `spark.a-bat.com`.

- **Strictly for syncing**: Your data is only used to send your profile to others.
- **Automatic Deletion**: If you do not appear online for 24 hours, your profile data is automatically removed from the server.
- **No tracking or AI**: SPARK does not use profile data for analytics, machine learning, or AI tools. Reported profiles may be reviewed only for moderation purposes.

When you view someone's profile, you save a copy of their profile to your PC.

### What data is shared?

SPARK only publishes your profile and presence data if you enable profile sharing. 

Presence is a mini-snippet of your profile, used for tooltips when hovering over a profile on the online list and populating a few fields.

**Profile Data**:

- GW2 Account Name
- Character Name
- Display Name (if different from character name)
- Profile ID (to know which one this is tied to on your characters if you have several and swap between them)
- Profile Name (the name you specifically put for the profile)
- RP Status (Online, Looking, Busy, Offline, etc.)
- Pronouns (if set)
- Character Race
- Character Profession
- Custom Profession (if set)
- Region (for filtering between NA and EU profiles)
- Current Location (if online and not set to be hidden)
- 'Currently' status (if set)
- Out of Character Info (if set)
- At a Glance Icons and custom tooltips/descriptions (if set)
- RP Experience (if set)
- RP Preferences (if set)
- RP Themes (if set)
- RP Styles (if set)
- 'Known for' info (a small RP hook you can optionally add)
- 'Description' info (your main profile box)

**Presence Data:**

- GW2 Account Name
- Character Name
- Display Name (if different from character name)
- RP Status (Online, Looking, Busy, Offline, etc.)
- 'Currently' status (if set)
- Out of Character Info (if set)
- Current Location (if online and not set to be hidden)
- Character Race
- Character Profession
- Custom Profession (if set)

**Nearby Players:**

- Character Name
- Race
- RP Status
- Map IP (last 3 digits)
- Distance from you

Nearby Players is an opt-in feature. Your information is not shared by default.

**Account Blocks:**

When you block an account, this is in a locally kept list on the module for players to manage. Block information is sent to the server in order to prevent a blocked user from seeing the person who blocked them as online, their location, or any profile updates.

Blocked users may have already viewed a profile and have a local copy on their machine. This is the only profile they will be able to view and will never see any updates or anything else from that GW2 Account unless unblocked.

### Reported Profiles / SPARK Moderation

SPARK retains a snapshot of a profile when it has been reported by users. Report reasons are appended to the profile, so if multiple people report a profile, their report messages are added into a server-only copy of the profile for review.

These snapshots are retained for moderation review purposes only. They are not exposed through the module API.

If the profile appears to be breaking any TOS or promotes hate/bigotry, they will be added to a ban list on the server. This prevents any uploads from their GW2 account to SPARK permanently until removed from the ban list.

If a SPARK user includes explicit information in a profile without marking it as 18+, SPARK may force all of that account's profiles to be marked as 18+ indefinitely.

## LICENSE

This project is licensed under the GNU General Public License v3.0 - see the [COPYING](COPYING) file for license details.