-- Uitvoeren in de database unitas_webshop als database- en schema-eigenaar.
-- Maak unitas_webshop_app en unitas_webshop_dba vooraf als LOGIN-rollen aan.
BEGIN;
REVOKE ALL ON DATABASE unitas_webshop FROM PUBLIC;
GRANT CONNECT ON DATABASE unitas_webshop TO unitas_webshop_app, unitas_webshop_dba;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO unitas_webshop_app, unitas_webshop_dba;
GRANT SELECT, INSERT, UPDATE ON public."AspNetUsers", public."AspNetUserTokens" TO unitas_webshop_app;
GRANT SELECT ON public."AspNetRoles", public."AspNetUserRoles",
    public."AspNetUserClaims", public."AspNetRoleClaims" TO unitas_webshop_app;
GRANT SELECT, INSERT, UPDATE ON public."AuthenticatorStates" TO unitas_webshop_app;
GRANT SELECT, INSERT ON public."QuoteRequests" TO unitas_webshop_app;
GRANT SELECT, INSERT ON public."CustomerOrders" TO unitas_webshop_app;
GRANT INSERT ON public."AuthEvents" TO unitas_webshop_app;
GRANT SELECT ("Id") ON public."AuthEvents" TO unitas_webshop_app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO unitas_webshop_app;
GRANT SELECT ON public.webshop_report, public.auth_report TO unitas_webshop_dba;
GRANT SELECT ("Id", "UserName", "RegisteredAt", "TwoFactorEnabled") ON public."AspNetUsers" TO unitas_webshop_dba;
COMMIT;
