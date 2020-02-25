global appProcess, appTitle, windowTitle

set windowTitle to "No Window"
tell application "System Events"
	# Get App Process and App Title
	set appProcess to first application process whose frontmost is true
	set appTitle to name of appProcess
	
	# Get app Window Title
	tell process appTitle
		if (count of windows) > 0 then
			set windowTitle to name of front window
			if exists (1st window whose value of attribute "AXMain" is true) then
				tell (1st window whose value of attribute "AXMain" is true)
					set windowTitle to value of attribute "AXTitle"
				end tell
			end if
		end if
	end tell
end tell

return {appTitle, windowTitle}