# To the maintainer

If you've just checked this out from source, this is an MVC2/.net 4 app. You'll also need log4net (1.2.10 is used), Jayrock (0.9.12915), and the Spark view generator (1.5.0.0, MVC2, NET4). Read and write permission to what will be configured as the "home" folder is required for logging and for references in templates.

To add a controller (which is what I'll expect should happen), remember the log string format and the check-for-authentication protocol. We don't use anything below debug or above error - warn on expected errors we have planned for, error on actual programming errors that should seem "impossible".

When generating HTML, note that there are both clientside and serverside versions of generators and DOM manipulators for both drop-down boxes and table rows. Serverside they exist in the form of InitXXX variables and Spark macros in Application.spark, clientside they exist in the CRUDtilities.

Don't add anything else other than the .sln and the files that appear in the Project Explorer to the repo - especially, especially, the dependencies or the log files.

Don't change the JS files used either (from normal to the min or vsdoc versions) unless you're willing to migrate hacks - marked with:
//
// HACK
//
over.