// HeatonCAMac.mm -- the native macOS menu bar for the HeatonCA Mac standalone
// player (the heaton-life / dynaface-unity pattern: Source~ next to the
// compiled .bundle, rebuilt with ./build.sh after editing this file).
//
// A Unity Mac player ships with only the stock app menu, so the app's own
// commands have nowhere to live: a Mac user reaches for the menu bar for
// About, Settings and Help before they look for an on-screen button. This
// bundle inserts them:
//
//   HeatonCA                      Help
//    +- About HeatonCA             +- Tutorial
//    +- Settings...      Cmd-,     +- HeatonCA Manual
//    +- --------
//    +- (Quit HeatonCA)  Cmd-Q     (only if the player has no Quit of its own)
//
// The bar communicates back to Unity through a REGISTERED FUNCTION POINTER,
// never UnitySendMessage -- UnitySendMessage is only supported on iOS/Android;
// a Standalone player does not export it at all, so calling it from this
// dlopen'd bundle throws DllNotFoundException the instant the plugin loads
// (the hard-won dynaface lesson, re-earned in heaton-life).
// NativeMenus.Mac.cs calls HeatonCA_RegisterCallback with an AOT-safe static
// C# delegate before HeatonCA_InstallMenuBar, and every handler below reports
// through that pointer. The C# side records the command and runs it from
// Poll() in the game loop, because a menu action fires inside AppKit's event
// dispatch, mid-frame, where app code must not run.
//
// Lifetime rule: HeatonCA_UninstallMenuBar clears the callback pointer FIRST.
// After the scripting runtime shuts down, a click that reached the registered
// delegate would be a native crash, so the pointer must be dead before the
// player tears the runtime down (AppController calls Uninstall from both
// OnApplicationQuit and OnDestroy; this file is idempotent for that reason).
//
// Strings crossing the boundary: every title is copied to an NSString inside
// the call that receives it. The const char* comes from P/Invoke marshaling
// and is only valid for the duration of the call -- the menu is built on a
// later run-loop turn, so a retained pointer would be a use-after-free.

#import <Cocoa/Cocoa.h>

typedef void (*HeatonCACallback)(const char *method, const char *arg);

// Written by HeatonCA_RegisterCallback, cleared by HeatonCA_UninstallMenuBar.
// Read on the main thread only (menu actions are dispatched there).
static HeatonCACallback gCallback = nullptr;

static void SendToUnity(const char *method)
{
    if (gCallback != nullptr)
        gCallback(method, "");
}

// The menu items' target. One instance for the life of the process; the menu
// items hold it weakly (NSMenuItem.target is a weak reference), which is why
// it is a strong global here.
@interface HeatonCAMenuTarget : NSObject
- (void)onAbout:(id)sender;
- (void)onSettings:(id)sender;
- (void)onQuit:(id)sender;
- (void)onManual:(id)sender;
- (void)onTutorial:(id)sender;
@end

@implementation HeatonCAMenuTarget

- (void)onAbout:(id)sender
{
    SendToUnity("About");
}

- (void)onSettings:(id)sender
{
    SendToUnity("Settings");
}

// Routed through Unity rather than NSApp terminate: so the quit follows the
// same path as every other exit -- Application.Quit runs OnApplicationQuit,
// which is where the app persists its settings and stops the evolve threads.
- (void)onQuit:(id)sender
{
    SendToUnity("Quit");
}

- (void)onManual:(id)sender
{
    SendToUnity("Manual");
}

- (void)onTutorial:(id)sender
{
    SendToUnity("Tutorial");
}

@end

static HeatonCAMenuTarget *gMenuTarget = nil;

// AppKit documents +[NSMenuItem separatorItem] as possibly returning a SHARED
// instance, and inserting an item that already belongs to a menu raises an
// exception -- which, on the menu bar, means the player dies at launch. So a
// separator is used only when it is not already placed, and a skipped one just
// leaves two groups adjacent.
static NSMenuItem *UnplacedSeparator(void)
{
    NSMenuItem *separator = [NSMenuItem separatorItem];
    return separator.menu == nil ? separator : nil;
}

// Everything we inserted, so Uninstall can take it back out again and leave
// the player's own menu bar as we found it.
static NSMutableArray<NSMenuItem *> *gInsertedItems = nil;
static NSMenu *gHelpMenu = nil;

static BOOL gInstalled = NO;   // guards the second Install call
static BOOL gCanceled = NO;    // set by Uninstall so a queued build block bails

extern "C" {

// Called once from NativeMenus.InstallPlatform, before HeatonCA_InstallMenuBar.
// The callback must be an AOT-safe static C# delegate: native code holds this
// raw pointer until Uninstall clears it.
void HeatonCA_RegisterCallback(HeatonCACallback callback)
{
    gCallback = callback;
}

// Called once from NativeMenus.InstallPlatform (AppController.Start).
// Idempotent. Titles are supplied by the C# side so every visible word stays
// in AppStrings; this file adds no user-facing text of its own except the
// "Help" menu title, which macOS itself keys on.
void HeatonCA_InstallMenuBar(const char *aboutTitle,
                             const char *settingsTitle,
                             const char *quitTitle,
                             const char *tutorialTitle,
                             const char *manualTitle)
{
    if (gInstalled)
        return;
    gInstalled = YES;
    gCanceled = NO;

    // Copy now: the incoming buffers belong to the marshaler and die with this
    // call, while the block below runs on a later run-loop turn.
    NSString *aboutStr = [NSString stringWithUTF8String:aboutTitle];
    NSString *settingsStr = [NSString stringWithUTF8String:settingsTitle];
    NSString *quitStr = [NSString stringWithUTF8String:quitTitle];
    NSString *tutorialStr = [NSString stringWithUTF8String:tutorialTitle];
    NSString *manualStr = [NSString stringWithUTF8String:manualTitle];

    if (gMenuTarget == nil)
        gMenuTarget = [[HeatonCAMenuTarget alloc] init];
    if (gInsertedItems == nil)
        gInsertedItems = [NSMutableArray array];

    // Deferred to the next main-queue turn: Unity builds its own menu bar
    // during player startup, and Start() can run before that bar is in place.
    // (This is the sequencing heaton-life ships.)
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gCanceled)
            return; // the app quit between Install and this turn

        NSMenu *mainMenu = [NSApp mainMenu];
        if (mainMenu == nil || mainMenu.numberOfItems == 0)
            return; // headless (-batchmode -nographics): there is no menu bar

        // ---- app menu ------------------------------------------------------
        // About and Settings go at the very top, in that order, which is where
        // a Mac user looks for them.
        NSMenu *appMenu = [mainMenu itemAtIndex:0].submenu;
        if (appMenu != nil)
        {
            // Drop the player's stock "About <app>" first: ours opens the real
            // About screen, and two About items in one menu is a bug report.
            for (NSMenuItem *item in [appMenu.itemArray copy])
            {
                if (item.action == @selector(orderFrontStandardAboutPanel:))
                    [appMenu removeItem:item];
            }

            NSMenuItem *aboutItem = [[NSMenuItem alloc] initWithTitle:aboutStr
                                                               action:@selector(onAbout:)
                                                        keyEquivalent:@""];
            aboutItem.target = gMenuTarget;
            [appMenu insertItem:aboutItem atIndex:0];
            [gInsertedItems addObject:aboutItem];

            // Cmd-, is the system-wide shortcut for Settings; a Mac user will
            // press it whether or not the item advertises it.
            NSMenuItem *settingsItem = [[NSMenuItem alloc] initWithTitle:settingsStr
                                                                  action:@selector(onSettings:)
                                                           keyEquivalent:@","];
            settingsItem.target = gMenuTarget;
            [appMenu insertItem:settingsItem atIndex:1];
            [gInsertedItems addObject:settingsItem];

            NSMenuItem *separator = UnplacedSeparator();
            if (separator != nil)
            {
                [appMenu insertItem:separator atIndex:2];
                [gInsertedItems addObject:separator];
            }

            // Quit: only if the player did not already provide one. Unity's
            // Mac player normally does, and a second Cmd-Q item would shadow
            // it; where it does not, the app menu would otherwise have no way
            // out at all.
            BOOL hasQuit = NO;
            for (NSMenuItem *item in appMenu.itemArray)
            {
                if (item.action == @selector(terminate:))
                {
                    hasQuit = YES;
                    break;
                }
            }
            if (!hasQuit)
            {
                NSMenuItem *quitSeparator = UnplacedSeparator();
                if (quitSeparator != nil)
                {
                    [appMenu addItem:quitSeparator];
                    [gInsertedItems addObject:quitSeparator];
                }

                NSMenuItem *quitItem = [[NSMenuItem alloc] initWithTitle:quitStr
                                                                  action:@selector(onQuit:)
                                                           keyEquivalent:@"q"];
                quitItem.target = gMenuTarget;
                [appMenu addItem:quitItem];
                [gInsertedItems addObject:quitItem];
            }
        }

        // ---- Help menu -----------------------------------------------------
        // Appended last and registered as the system Help menu, which gives it
        // macOS's own menu-search field for free.
        gHelpMenu = [[NSMenu alloc] initWithTitle:@"Help"];

        // Tutorial before Manual, the order the About screen already lists them
        // in and the order the Windows bar uses.
        NSMenuItem *tutorialItem = [[NSMenuItem alloc] initWithTitle:tutorialStr
                                                              action:@selector(onTutorial:)
                                                       keyEquivalent:@""];
        tutorialItem.target = gMenuTarget;
        [gHelpMenu addItem:tutorialItem];

        NSMenuItem *manualItem = [[NSMenuItem alloc] initWithTitle:manualStr
                                                            action:@selector(onManual:)
                                                     keyEquivalent:@""];
        manualItem.target = gMenuTarget;
        [gHelpMenu addItem:manualItem];

        NSMenuItem *helpMenuItem = [[NSMenuItem alloc] init];
        helpMenuItem.submenu = gHelpMenu;
        [mainMenu addItem:helpMenuItem];
        [gInsertedItems addObject:helpMenuItem];
        NSApp.helpMenu = gHelpMenu;
    });
}

// Called from NativeMenus.UninstallPlatform (AppController.OnApplicationQuit
// and OnDestroy), on Unity's main thread -- which is AppKit's main thread, so
// the menu surgery below is safe to do inline. Idempotent.
//
// Clearing gCallback is the part that matters and happens first, whatever
// thread we are on: after the scripting runtime shuts down, a menu click that
// called through the registered delegate would crash the process.
void HeatonCA_UninstallMenuBar(void)
{
    gCallback = nullptr;
    gCanceled = YES;
    if (!gInstalled)
        return;
    gInstalled = NO;

    if (![NSThread isMainThread])
        return; // cannot touch AppKit from here; the dead callback is enough

    if (NSApp.helpMenu != nil && NSApp.helpMenu == gHelpMenu)
        NSApp.helpMenu = nil;
    gHelpMenu = nil;

    for (NSMenuItem *item in gInsertedItems)
    {
        item.target = nil;
        [item.menu removeItem:item];
    }
    [gInsertedItems removeAllObjects];
    gMenuTarget = nil;
}

// Show a just-saved file to the user, selected in a Finder window (the
// dynaface export habit). The Mac App Store build is sandboxed, so snapshots
// land in the app's container -- a path nobody browses to by hand.
//
// This is here rather than in C# because the sandbox blocks
// Process.Start("open", ...): spawning /usr/bin/open from inside the sandbox
// is denied, while NSWorkspace's activateFileViewerSelectingURLs is a service
// request Finder (its own process, outside our container) fulfills.
void HeatonCA_RevealInFinder(const char *path)
{
    if (path == NULL)
        return;
    @autoreleasepool
    {
        NSString *string = [NSString stringWithUTF8String:path];
        if (string == nil)
            return;
        NSURL *url = [NSURL fileURLWithPath:string];
        [[NSWorkspace sharedWorkspace] activateFileViewerSelectingURLs:@[ url ]];
    }
}

} // extern "C"
