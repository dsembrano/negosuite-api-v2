# API route inventory

Generated from the running .NET 6 application on 2026-09-25. See openapi.json for request schemas and declared response schemas. Swagger metadata does not prove runtime authorization, actual response fields, or database behavior.

| Method | Path | Parameters | Declared statuses |
| --- | --- | --- | --- |
| GET | /api/account-categories | criteria (query) | 200 |
| POST | /api/account-categories |  () | 200 |
| DELETE | /api/account-categories/{id} | id (path) | 200 |
| GET | /api/account-categories/{id} | id (path) | 200 |
| PUT | /api/account-categories/{id} | id (path) | 200 |
| GET | /api/accounts | criteria (query) | 200 |
| POST | /api/accounts |  () | 200 |
| DELETE | /api/accounts/{id} | id (path) | 200 |
| GET | /api/accounts/{id} | id (path) | 200 |
| PUT | /api/accounts/{id} | id (path) | 200 |
| GET | /api/accounts/header |  () | 200 |
| GET | /api/accounts/link |  () | 200 |
| POST | /api/ai/query |  () | 200 |
| GET | /api/ai/test |  () | 200 |
| GET | /api/auth/app-version |  () | 200 |
| POST | /api/auth/change-password |  () | 200 |
| GET | /api/auth/metabase-token | userConfigId (query) | 200 |
| POST | /api/auth/refresh-access-token |  () | 200 |
| POST | /api/auth/reset-password |  () | 200 |
| POST | /api/auth/sign-in |  () | 200 |
| GET | /api/bill-payments | criteria (query) | 200 |
| POST | /api/bill-payments |  () | 200 |
| DELETE | /api/bill-payments/{id} | id (path) | 200 |
| GET | /api/bill-payments/{id} | id (path) | 200 |
| PUT | /api/bill-payments/{id} | id (path) | 200 |
| GET | /api/bills | criteria (query) | 200 |
| POST | /api/bills |  () | 200 |
| DELETE | /api/bills/{id} | id (path) | 200 |
| GET | /api/bills/{id} | id (path) | 200 |
| PUT | /api/bills/{id} | id (path) | 200 |
| GET | /api/chats | userId (query) | 200 |
| DELETE | /api/chats/{id} | id (path) | 200 |
| PUT | /api/chats/{id} | id (path) | 200 |
| GET | /api/chats/messages | chatSessionId (query) | 200 |
| GET | /api/city-municipalities |  () | 200 |
| POST | /api/city-municipalities |  () | 200 |
| DELETE | /api/city-municipalities/{id} | id (path) | 200 |
| GET | /api/city-municipalities/{id} | id (path) | 200 |
| PUT | /api/city-municipalities/{id} | id (path) | 200 |
| GET | /api/configs |  () | 200 |
| POST | /api/configs |  () | 200 |
| DELETE | /api/configs/{id} | id (path) | 200 |
| GET | /api/configs/{id} | id (path) | 200 |
| PUT | /api/configs/{id} | id (path) | 200 |
| PUT | /api/configs/payment-adjustment-type/{id} | id (path) | 200 |
| GET | /api/configs/template |  () | 200 |
| GET | /api/countries |  () | 200 |
| POST | /api/countries |  () | 200 |
| DELETE | /api/countries/{id} | id (path) | 200 |
| GET | /api/countries/{id} | id (path) | 200 |
| PUT | /api/countries/{id} | id (path) | 200 |
| GET | /api/currencies |  () | 200 |
| POST | /api/currencies |  () | 200 |
| DELETE | /api/currencies/{id} | id (path) | 200 |
| GET | /api/currencies/{id} | id (path) | 200 |
| PUT | /api/currencies/{id} | id (path) | 200 |
| GET | /api/currencies/base |  () | 200 |
| GET | /api/customers | criteria (query) | 200 |
| POST | /api/customers |  () | 200 |
| DELETE | /api/customers/{id} | id (path) | 200 |
| GET | /api/customers/{id} | id (path) | 200 |
| PUT | /api/customers/{id} | id (path) | 200 |
| GET | /api/discount-types | criteria (query) | 200 |
| POST | /api/discount-types |  () | 200 |
| DELETE | /api/discount-types/{id} | id (path) | 200 |
| GET | /api/discount-types/{id} | id (path) | 200 |
| PUT | /api/discount-types/{id} | id (path) | 200 |
| POST | /api/discount-types/many |  () | 200 |
| GET | /api/download-apk |  () | 200 |
| POST | /api/email |  () | 200 |
| POST | /api/email/email-confirmation |  () | 200 |
| GET | /api/email/log/{uuid} | uuid (path) | 200 |
| POST | /api/email/member-invite |  () | 200 |
| POST | /api/email/password-reset |  () | 200 |
| GET | /api/expense-payments | criteria (query) | 200 |
| POST | /api/expense-payments |  () | 200 |
| DELETE | /api/expense-payments/{id} | id (path) | 200 |
| GET | /api/expense-payments/{id} | id (path) | 200 |
| PUT | /api/expense-payments/{id} | id (path) | 200 |
| GET | /api/financial-reports/gl-details | criteria (query) | 200 |
| GET | /api/financial-reports/gl-summary | criteria (query) | 200 |
| GET | /api/financial-reports/income-statement | criteria (query) | 200 |
| GET | /api/financial-reports/journal-transactions | criteria (query) | 200 |
| GET | /api/financial-reports/journal-transactions-rc | criteria (query) | 200 |
| GET | /api/financial-reports/trial-balance | criteria (query) | 200 |
| GET | /api/general-journals | criteria (query) | 200 |
| POST | /api/general-journals |  () | 200 |
| DELETE | /api/general-journals/{id} | id (path) | 200 |
| GET | /api/general-journals/{id} | id (path) | 200 |
| PUT | /api/general-journals/{id} | id (path) | 200 |
| GET | /api/industries |  () | 200 |
| POST | /api/industries |  () | 200 |
| DELETE | /api/industries/{id} | id (path) | 200 |
| GET | /api/industries/{id} | id (path) | 200 |
| PUT | /api/industries/{id} | id (path) | 200 |
| GET | /api/inventory-adjustments | criteria (query) | 200 |
| POST | /api/inventory-adjustments |  () | 200 |
| DELETE | /api/inventory-adjustments/{id} | id (path) | 200 |
| GET | /api/inventory-adjustments/{id} | id (path) | 200 |
| PUT | /api/inventory-adjustments/{id} | id (path) | 200 |
| GET | /api/inventory-locations | criteria (query) | 200 |
| POST | /api/inventory-locations |  () | 200 |
| DELETE | /api/inventory-locations/{id} | id (path) | 200 |
| GET | /api/inventory-locations/{id} | id (path) | 200 |
| PUT | /api/inventory-locations/{id} | id (path) | 200 |
| GET | /api/inventory-reports/inventory-by-location | criteria (query) | 200 |
| GET | /api/inventory-reports/inventory-transactions | criteria (query) | 200 |
| GET | /api/inventory-reports/low-inventory | criteria (query) | 200 |
| GET | /api/inventory-reports/stock-summary | criteria (query) | 200 |
| GET | /api/item-categories | criteria (query) | 200 |
| POST | /api/item-categories |  () | 200 |
| DELETE | /api/item-categories/{id} | id (path) | 200 |
| GET | /api/item-categories/{id} | id (path) | 200 |
| PUT | /api/item-categories/{id} | id (path) | 200 |
| GET | /api/items | criteria (query) | 200 |
| POST | /api/items |  () | 200 |
| DELETE | /api/items/{id} | id (path) | 200 |
| GET | /api/items/{id} | id (path) | 200 |
| PUT | /api/items/{id} | id (path) | 200 |
| GET | /api/items/average-cost/{id} | id (path) | 200 |
| GET | /api/items/units | criteria (query) | 200 |
| GET | /api/journal-entries/unapplied-ar-credits | criteria (query) | 200 |
| GET | /api/journal-entries/unapplied-ar-credits/{id} | id (path) | 200 |
| GET | /api/journal-entries/unpaid-bills | criteria (query) | 200 |
| GET | /api/journal-entries/unpaid-invoices | criteria (query) | 200 |
| GET | /api/NavigationItems |  () | 200 |
| POST | /api/NavigationItems |  () | 200 |
| DELETE | /api/NavigationItems/{id} | id (path) | 200 |
| GET | /api/NavigationItems/{id} | id (path) | 200 |
| PUT | /api/NavigationItems/{id} | id (path) | 200 |
| GET | /api/payable-reports/aging-details | criteria (query) | 200 |
| GET | /api/payable-reports/aging-summary | criteria (query) | 200 |
| GET | /api/payable-reports/supplier-balances | criteria (query) | 200 |
| GET | /api/payable-reports/supplier-balances-rc | criteria (query) | 200 |
| GET | /api/payment-modes |  () | 200 |
| POST | /api/payment-modes |  () | 200 |
| DELETE | /api/payment-modes/{id} | id (path) | 200 |
| GET | /api/payment-modes/{id} | id (path) | 200 |
| PUT | /api/payment-modes/{id} | id (path) | 200 |
| GET | /api/payments | criteria (query) | 200 |
| POST | /api/payments |  () | 200 |
| DELETE | /api/payments/{id} | id (path) | 200 |
| GET | /api/payments/{id} | id (path) | 200 |
| PUT | /api/payments/{id} | id (path) | 200 |
| POST | /api/payments/bill |  () | 200 |
| DELETE | /api/payments/bill/{id} | id (path) | 200 |
| PUT | /api/payments/bill/{id} | id (path) | 200 |
| GET | /api/payment-terms |  () | 200 |
| POST | /api/payment-terms |  () | 200 |
| DELETE | /api/payment-terms/{id} | id (path) | 200 |
| GET | /api/payment-terms/{id} | id (path) | 200 |
| PUT | /api/payment-terms/{id} | id (path) | 200 |
| GET | /api/receivable-reports/aging-details | criteria (query) | 200 |
| GET | /api/receivable-reports/aging-details-rc | criteria (query) | 200 |
| GET | /api/receivable-reports/aging-summary | criteria (query) | 200 |
| GET | /api/receivable-reports/aging-summary-rc | criteria (query) | 200 |
| GET | /api/receivable-reports/customer-balances | criteria (query) | 200 |
| GET | /api/receivable-reports/customer-balances-rc | criteria (query) | 200 |
| GET | /api/receiving-reports | criteria (query) | 200 |
| POST | /api/receiving-reports |  () | 200 |
| DELETE | /api/receiving-reports/{id} | id (path) | 200 |
| GET | /api/receiving-reports/{id} | id (path) | 200 |
| PUT | /api/receiving-reports/{id} | id (path) | 200 |
| GET | /api/responsibility-centers | criteria (query) | 200 |
| POST | /api/responsibility-centers |  () | 200 |
| DELETE | /api/responsibility-centers/{id} | id (path) | 200 |
| GET | /api/responsibility-centers/{id} | id (path) | 200 |
| PUT | /api/responsibility-centers/{id} | id (path) | 200 |
| GET | /api/responsibility-centers/type | criteria (query) | 200 |
| GET | /api/responsibility-center-types | criteria (query) | 200 |
| POST | /api/responsibility-center-types |  () | 200 |
| DELETE | /api/responsibility-center-types/{id} | id (path) | 200 |
| GET | /api/responsibility-center-types/{id} | id (path) | 200 |
| PUT | /api/responsibility-center-types/{id} | id (path) | 200 |
| POST | /api/responsibility-center-types/many |  () | 200 |
| GET | /api/sales-invoice-payments | criteria (query) | 200 |
| POST | /api/sales-invoice-payments |  () | 200 |
| DELETE | /api/sales-invoice-payments/{id} | id (path) | 200 |
| GET | /api/sales-invoice-payments/{id} | id (path) | 200 |
| PUT | /api/sales-invoice-payments/{id} | id (path) | 200 |
| GET | /api/sales-invoices | criteria (query) | 200 |
| POST | /api/sales-invoices |  () | 200 |
| DELETE | /api/sales-invoices/{id} | id (path) | 200 |
| GET | /api/sales-invoices/{id} | id (path) | 200 |
| PUT | /api/sales-invoices/{id} | id (path) | 200 |
| GET | /api/sales-receipts | criteria (query) | 200 |
| POST | /api/sales-receipts |  () | 200 |
| DELETE | /api/sales-receipts/{id} | id (path) | 200 |
| GET | /api/sales-receipts/{id} | id (path) | 200 |
| PUT | /api/sales-receipts/{id} | id (path) | 200 |
| GET | /api/sales-reports/sales-by-customer | criteria (query) | 200 |
| GET | /api/sales-reports/sales-by-item | criteria (query) | 200 |
| GET | /api/sales-reports/sales-monthly-trend | criteria (query) | 200 |
| GET | /api/sales-reports/sales-transaction-details-rc | criteria (query) | 200 |
| GET | /api/sales-reports/sales-transactions | criteria (query) | 200 |
| GET | /api/sales-reports/sales-transactions-rc | criteria (query) | 200 |
| GET | /api/stock-issuances | criteria (query) | 200 |
| POST | /api/stock-issuances |  () | 200 |
| DELETE | /api/stock-issuances/{id} | id (path) | 200 |
| GET | /api/stock-issuances/{id} | id (path) | 200 |
| PUT | /api/stock-issuances/{id} | id (path) | 200 |
| GET | /api/stock-transfers | criteria (query) | 200 |
| POST | /api/stock-transfers |  () | 200 |
| DELETE | /api/stock-transfers/{id} | id (path) | 200 |
| GET | /api/stock-transfers/{id} | id (path) | 200 |
| PUT | /api/stock-transfers/{id} | id (path) | 200 |
| GET | /api/subscription-plans |  () | 200 |
| GET | /api/suppliers | criteria (query) | 200 |
| POST | /api/suppliers |  () | 200 |
| DELETE | /api/suppliers/{id} | id (path) | 200 |
| GET | /api/suppliers/{id} | id (path) | 200 |
| PUT | /api/suppliers/{id} | id (path) | 200 |
| GET | /api/tax-rates | criteria (query) | 200 |
| POST | /api/tax-rates |  () | 200 |
| DELETE | /api/tax-rates/{id} | id (path) | 200 |
| GET | /api/tax-rates/{id} | id (path) | 200 |
| PUT | /api/tax-rates/{id} | id (path) | 200 |
| POST | /api/tax-rates/many |  () | 200 |
| GET | /api/user-roles | criteria (query) | 200 |
| POST | /api/user-roles |  () | 200 |
| DELETE | /api/user-roles/{id} | id (path) | 200 |
| GET | /api/user-roles/{id} | id (path) | 200 |
| PUT | /api/user-roles/{id} | id (path) | 200 |
| GET | /api/users |  () | 200 |
| POST | /api/users |  () | 200 |
| DELETE | /api/users/{id} | id (path) | 200 |
| GET | /api/users/{id} | id (path) | 200 |
| PUT | /api/users/{id} | id (path) | 200 |
| GET | /api/users/config | criteria (query) | 200 |
| GET | /api/users/config-can-add | configId (query) | 200 |
| POST | /api/users/new-account |  () | 200 |
| GET | /api/users/roles | criteria (query) | 200 |
| PUT | /api/users/update-config | id (query) | 200 |
| PUT | /api/users/update-config-template | id (query) | 200 |
| PUT | /api/users/update-role | id (query), userRoleId (query) | 200 |
| PUT | /api/users/update-ui-config | id (query) | 200 |
