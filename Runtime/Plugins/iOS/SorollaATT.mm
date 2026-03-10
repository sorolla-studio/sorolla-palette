#import <AppTrackingTransparency/AppTrackingTransparency.h>

// Native ATT bridge — replaces com.unity.ads.ios-support dependency.
// Status values: 0=NotDetermined, 1=Restricted, 2=Denied, 3=Authorized

typedef void (*ATTCallback)(int status);

extern "C" {

int _SorollaATT_GetStatus() {
    if (@available(iOS 14, *)) {
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    }
    return 3; // Pre-iOS 14: always authorized
}

void _SorollaATT_RequestAuthorization(ATTCallback callback) {
    if (@available(iOS 14, *)) {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            if (callback) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    callback((int)status);
                });
            }
        }];
    } else {
        if (callback) {
            callback(3); // Pre-iOS 14: always authorized
        }
    }
}

} // extern "C"
