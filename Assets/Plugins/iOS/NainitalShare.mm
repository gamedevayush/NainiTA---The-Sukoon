// Share sheet for bug reports on the iPhone (BugReporter.Save): the note text plus screen.png and note.txt,
// so a report can go out by WhatsApp, Mail, AirDrop or "Save to Files" right after it is saved.
#import <UIKit/UIKit.h>

UIViewController* UnityGetGLViewController();

extern "C" void _NainitalShareFiles(const char* paths, const char* text)
{
    NSMutableArray* items = [NSMutableArray array];
    if (text && strlen(text) > 0) [items addObject:[NSString stringWithUTF8String:text]];
    if (paths)
    {
        NSString* joined = [NSString stringWithUTF8String:paths];
        for (NSString* p in [joined componentsSeparatedByString:@"\n"])
            if (p.length > 0 && [[NSFileManager defaultManager] fileExistsAtPath:p])
                [items addObject:[NSURL fileURLWithPath:p]];
    }
    if (items.count == 0) return;

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* vc = UnityGetGLViewController();
        if (!vc) return;
        UIActivityViewController* sheet = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIPopoverPresentationController* pop = sheet.popoverPresentationController;
        if (pop)   // iPad: a share sheet must hang from something
        {
            pop.sourceView = vc.view;
            pop.sourceRect = CGRectMake(vc.view.bounds.size.width / 2, vc.view.bounds.size.height / 2, 1, 1);
            pop.permittedArrowDirections = 0;
        }
        [vc presentViewController:sheet animated:YES completion:nil];
    });
}
