#import <UserMessagingPlatform/UserMessagingPlatform.h>

// Google UMP bridge: the consent information update, the consent form when UMP requires one, and the
// privacy options form. UMP methods must be called on the main thread, which is where Unity calls these.
// The callback receives NULL on success, else the error description, always on the main queue.

typedef void (*UMPCallback)(const char *error);

static void SorollaUMP_Complete(UMPCallback callback, NSError *error) {
    if (!callback) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        callback(error ? error.localizedDescription.UTF8String : NULL);
    });
}

extern "C" {

void _SorollaUMP_Gather(UMPCallback callback) {
    UMPRequestParameters *parameters = [[UMPRequestParameters alloc] init];
    [UMPConsentInformation.sharedInstance requestConsentInfoUpdateWithParameters:parameters
                                                               completionHandler:^(NSError *updateError) {
        if (updateError) {
            SorollaUMP_Complete(callback, updateError);
            return;
        }
        // The header requires the main queue for the form call but names no queue for this handler.
        dispatch_async(dispatch_get_main_queue(), ^{
            [UMPConsentForm loadAndPresentIfRequiredFromViewController:nil
                                                     completionHandler:^(NSError *formError) {
                SorollaUMP_Complete(callback, formError);
            }];
        });
    }];
}

int _SorollaUMP_PrivacyOptionsRequired() {
    return UMPConsentInformation.sharedInstance.privacyOptionsRequirementStatus
        == UMPPrivacyOptionsRequirementStatusRequired ? 1 : 0;
}

void _SorollaUMP_ShowPrivacyOptions(UMPCallback callback) {
    [UMPConsentForm presentPrivacyOptionsFormFromViewController:nil
                                              completionHandler:^(NSError *error) {
        SorollaUMP_Complete(callback, error);
    }];
}

} // extern "C"
