Bongo Cat Key Stats
===================
Install : quit the game, double-click patch.bat. (.NET 9 runtime is required; the game is found via Steam automatically,
          otherwise drag the game folder onto patch.bat.)
          Works on the official Assembly-CSharp.dll and on one that another mod already changed - only
          GlobalKeyHook.Awake gets one extra guarded call; nothing else in the DLL is touched.
Remove  : double-click restore.bat (puts back Assembly-CSharp.dll.keystats-original, deletes BongoKeyStats.dll).
          Your data in <game folder>\KeyStats\ is never deleted.
After a game update : run patch.bat again.
In game : Settings > "按键统计" tab (below the first tab). Shows totals, top 5, full panel with keyboard heat map, HTML export.
Data    : <game folder>\KeyStats\keystats.json (+ .bak, report.html). Plain JSON. Counts only - no key order, no text, no network.
Log     : search Player.log for "[BongoKeyStats]".
