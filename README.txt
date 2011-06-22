# To the maintainer

If you've just checked this out from source, this is an MVC2/.net 4 app. You'll also need log4net (1.2.10 is used) and the Spark view generator (1.5.0.0, MVC2, NET4). Read and write permission to what will be configured as the "home" folder is required for logging and for references in templates.

To add a controller (which is what I'll expect should happen), remember the log string format and the check-for-authentication protocol. We don't use anything below debug or above error - warn on expected errors we have planned for, error on actual programming errors that should seem "impossible".

Don't add anything else other than the .sln and the files that appear in the Project Explorer to the repo - especially, especially, the dependencies or the log files.