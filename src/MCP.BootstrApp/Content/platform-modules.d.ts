/** DataForm Action Response */
export interface DataFormActionResponse {
    /** Requested Action */
    Action: string;
    /** Object Ids affected by action */
    Ids?: string[];
    /** Result summary or error message */
    Message?: string;
    /** Suggested Next Url */
    NextUrl?: string;
    RunId?: string;
    /** Whether the operation successful */
    Success: boolean;
}

/** System: Response Body on API Error */
export interface api_pi_response_error {
    /** HTTP Response Status Code */
    statusCode?: number;
    /** API Error Message */
    message?: string;
    /** Whether the request was successful. Always false */
    success?: boolean;
}

declare module "session" {
    export interface User {
        id: string;
        name: string;
    }

    /** get current user for this session */
    export function currUser(): User;
}