https://stripe.com/docs/testing
https://stripe.com/docs/payments/payment-intents/migration#saving-cards
https://stripe.com/docs/payments/setup-intents
https://stripe.com/docs/payments/save-and-reuse#web
https://stripe.com/docs/payments/bancontact/accept-a-payment
https://stripe.com/docs/ach

```
{
  "id": "seti_1GzY3fGpN4AIlxGgFPhM10Z0",
  "object": "setup_intent",
  "application": null,
  "client_secret": "seti_1GzY3fGpN4AIlxGgFPhM10Z0_secret_HYfOG3nYvVwpvfNP7Y0hx7454i5c0AP",
  "created": 1593479687,
  "customer": "cus_HYdWYiADIOIQoK",
  "livemode": false,
  "mandate": null,
  "metadata": {},
  "on_behalf_of": null,
  "payment_method": null,
  "payment_method_options": {
    "card": {
      "request_three_d_secure": "automatic"
    }
  },
  "payment_method_types": [
    "card"
  ],
  "single_use_mandate": null,
  "status": "requires_payment_method",
  "usage": "off_session"
}
```

```
{
  "id": "seti_1GzY3fGpN4AIlxGgFPhM10Z0",
  "object": "setup_intent",
  "cancellation_reason": null,
  "client_secret": "seti_1GzY3fGpN4AIlxGgFPhM10Z0_secret_HYfOG3nYvVwpvfNP7Y0hx7454i5c0AP",
  "created": 1593479687,
  "description": null,
  "last_setup_error": null,
  "livemode": false,
  "next_action": null,
  "payment_method": "pm_1GzYE4GpN4AIlxGgETRerqTb",
  "payment_method_types": [
    "card"
  ],
  "status": "succeeded",
  "usage": "off_session"
}
```

```
{
  "id": "pm_1GzYE4GpN4AIlxGgETRerqTb",
  "object": "payment_method",
  "billing_details": {
    "address": {
      "postal_code": "12345"
    }
  },
  "card": {
    "brand": "visa",
    "checks": {
      "address_postal_code_check": "pass",
      "cvc_check": "pass"
    },
    "country": "US",
    "exp_month": 12,
    "exp_year": 2023,
    "fingerprint": "uBKlDyqjJLC0ZosC",
    "funding": "credit",
    "last4": "4242",
    "networks": {
      "available": [
        "visa"
      ]
    },
    "three_d_secure_usage": {
      "supported": true
    }
  },
  "created": 1593480333,
  "customer": "cus_HYdWYiADIOIQoK",
  "livemode": false,
  "metadata": {},
  "type": "card"
}
```