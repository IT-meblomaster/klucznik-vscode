-- phpMyAdmin SQL Dump
-- version 5.2.3
-- https://www.phpmyadmin.net/
--
-- Host: localhost
-- Generation Time: Lip 08, 2026 at 05:03 AM
-- Wersja serwera: 10.3.39-MariaDB
-- Wersja PHP: 8.2.30

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Baza danych: `klucznik`
--

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `app_settings`
--

CREATE TABLE `app_settings` (
  `setting_key` varchar(100) NOT NULL,
  `setting_value` longtext NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


--
-- Struktura tabeli dla tabeli `buildings`
--

CREATE TABLE `buildings` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(100) NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `buildings`
--

--
-- Struktura tabeli dla tabeli `keys`
--

CREATE TABLE `keys` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(150) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `building_id` int(10) UNSIGNED NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `zawieszka` varchar(25) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Struktura tabeli dla tabeli `key_loans`
--

CREATE TABLE `key_loans` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `key_id` int(10) UNSIGNED NOT NULL,
  `rfid_tag_id` int(10) UNSIGNED DEFAULT NULL,
  `issued_to_card` varchar(64) NOT NULL,
  `issued_to_name` varchar(255) NOT NULL,
  `issued_at` datetime NOT NULL DEFAULT current_timestamp(),
  `returned_by_card` varchar(64) DEFAULT NULL,
  `returned_by_name` varchar(255) DEFAULT NULL,
  `returned_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `key_logs`
--

CREATE TABLE `key_logs` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `key_id` int(10) UNSIGNED NOT NULL,
  `rfid_tag_id` int(10) UNSIGNED DEFAULT NULL,
  `action_type` varchar(50) NOT NULL,
  `action_details` varchar(1000) DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `key_rfid_assignments`
--

CREATE TABLE `key_rfid_assignments` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `key_id` int(10) UNSIGNED NOT NULL,
  `rfid_tag_id` int(10) UNSIGNED NOT NULL,
  `assigned_from` datetime NOT NULL DEFAULT current_timestamp(),
  `assigned_to` datetime DEFAULT NULL,
  `assigned_by` varchar(255) DEFAULT NULL,
  `unassigned_reason` varchar(255) DEFAULT NULL,
  `notes` varchar(500) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `menu_items`
--

CREATE TABLE `menu_items` (
  `id` int(10) UNSIGNED NOT NULL,
  `parent_id` int(10) UNSIGNED DEFAULT NULL,
  `page_id` int(10) UNSIGNED DEFAULT NULL,
  `label` varchar(150) NOT NULL,
  `url` varchar(500) DEFAULT NULL,
  `menu_group` varchar(100) NOT NULL DEFAULT 'main',
  `target` varchar(20) NOT NULL DEFAULT '_self',
  `sort_order` int(11) NOT NULL DEFAULT 100,
  `is_visible` tinyint(1) NOT NULL DEFAULT 1,
  `is_system` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `menu_items`
--

INSERT INTO `menu_items` (`id`, `parent_id`, `page_id`, `label`, `url`, `menu_group`, `target`, `sort_order`, `is_visible`, `is_system`, `created_at`, `updated_at`) VALUES
(1, NULL, 5, 'Dashboard', NULL, 'main', '_self', 10, 1, 0, '2026-04-17 08:44:22', '2026-04-30 07:59:49'),
(2, NULL, NULL, 'Ustawienia', 'internal:container:ustawienia', 'main', '_self', 100, 1, 0, '2026-04-17 08:44:22', '2026-04-30 07:59:59'),
(4, 2, 6, 'Użytkownicy', NULL, 'main', '_self', 10, 1, 0, '2026-04-17 08:44:22', '2026-04-30 08:03:35'),
(5, 2, 28, 'Role', NULL, 'main', '_self', 20, 1, 0, '2026-04-17 08:44:22', '2026-07-03 11:10:35'),
(6, 2, 8, 'Uprawnienia', NULL, 'main', '_self', 30, 1, 0, '2026-04-17 08:44:22', '2026-04-30 08:03:54'),
(7, 2, 9, 'Menu', NULL, 'main', '_self', 40, 1, 0, '2026-04-17 08:44:22', '2026-04-30 08:03:46'),
(9, 2, NULL, 'phpMyAdmin', 'http://patrol/pma/index.php', 'main', '_blank', 50, 1, 0, '2026-04-17 11:23:49', '2026-04-17 11:23:49'),
(10, 2, NULL, 'Separator', 'internal:separator', 'main', '_self', 35, 1, 0, '2026-04-20 09:51:50', '2026-04-20 09:52:25'),
(11, 2, NULL, 'Separator', 'internal:separator', 'main', '_self', 45, 1, 0, '2026-04-20 09:52:54', '2026-04-20 09:52:54'),
(12, 2, 10, 'Strony', NULL, 'main', '_self', 39, 1, 0, '2026-04-20 11:35:21', '2026-04-20 11:37:07'),
(25, NULL, NULL, 'Klucze', NULL, 'main', '_self', 20, 1, 0, '2026-07-06 06:35:58', '2026-07-06 06:35:58'),
(26, 25, 31, 'Dostępność kluczy', NULL, 'main', '_self', 10, 1, 0, '2026-07-06 06:35:58', '2026-07-06 06:35:58'),
(27, 25, 32, 'Klucze', NULL, 'main', '_self', 30, 1, 0, '2026-07-06 06:43:20', '2026-07-07 10:53:19'),
(28, 25, 33, 'Logi', NULL, 'main', '_self', 40, 1, 0, '2026-07-06 06:43:20', '2026-07-07 10:53:12'),
(29, 25, 34, 'Budynki', NULL, 'main', '_self', 20, 1, 0, '2026-07-07 10:53:48', '2026-07-07 10:54:08');

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `menu_item_permissions`
--

CREATE TABLE `menu_item_permissions` (
  `menu_item_id` int(10) UNSIGNED NOT NULL,
  `permission_id` int(10) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `menu_item_permissions`
--

INSERT INTO `menu_item_permissions` (`menu_item_id`, `permission_id`) VALUES
(1, 16),
(2, 25),
(4, 26),
(5, 27),
(6, 28),
(7, 30),
(9, 31),
(10, 25),
(11, 25),
(12, 29);

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `pages`
--

CREATE TABLE `pages` (
  `id` int(10) UNSIGNED NOT NULL,
  `slug` varchar(100) NOT NULL,
  `title` varchar(150) NOT NULL,
  `file_path` varchar(255) NOT NULL,
  `is_public` tinyint(1) NOT NULL DEFAULT 0,
  `is_system` tinyint(1) NOT NULL DEFAULT 0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `pages`
--

INSERT INTO `pages` (`id`, `slug`, `title`, `file_path`, `is_public`, `is_system`, `is_active`, `created_at`, `updated_at`) VALUES
(1, 'home', 'Strona główna', 'pages/home.php', 1, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:11:33'),
(2, 'login', 'Logowanie', 'pages/login.php', 1, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:11:26'),
(3, 'logout', 'Wylogowanie', 'pages/logout.php', 0, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:11:02'),
(4, 'forbidden', 'Brak dostępu', 'pages/forbidden.php', 1, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:10:50'),
(5, 'dashboard', 'Dashboard', 'pages/dashboard.php', 0, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:10:41'),
(6, 'users', 'Użytkownicy', 'pages/users.php', 0, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:10:29'),
(8, 'permissions', 'Uprawnienia', 'pages/permissions.php', 0, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:10:13'),
(9, 'menu_manager', 'Menedżer menu', 'pages/menu_manager.php', 0, 0, 1, '2026-04-17 08:44:22', '2026-04-30 08:10:00'),
(10, 'pages_manager', 'Zarządzanie stronami', 'pages/pages_manager.php', 0, 0, 1, '2026-04-20 11:35:21', '2026-04-20 11:35:21'),
(24, 'change_password', 'Zmiana hasła', 'pages/change_password.php', 0, 0, 1, '2026-05-13 12:02:50', '2026-05-13 12:02:50'),
(28, 'roles', 'Role', 'pages/roles.php', 0, 0, 1, '2026-07-03 11:00:09', '2026-07-03 11:00:09'),
(31, 'key_inventory', 'Dostępność kluczy', 'pages/key_inventory.php', 0, 0, 1, '2026-07-06 06:35:58', '2026-07-06 06:35:58'),
(32, 'keys', 'Klucze', 'pages/keys.php', 0, 0, 1, '2026-07-06 06:43:20', '2026-07-06 06:43:20'),
(33, 'key_logs', 'Logi', 'pages/key_logs.php', 0, 0, 1, '2026-07-06 06:43:20', '2026-07-06 06:43:20'),
(34, 'buildings', 'Budynki', 'pages/buildings.php', 0, 0, 1, '2026-07-07 10:52:38', '2026-07-07 10:52:38');

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `page_permissions`
--

CREATE TABLE `page_permissions` (
  `page_id` int(10) UNSIGNED NOT NULL,
  `permission_id` int(10) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `page_permissions`
--

INSERT INTO `page_permissions` (`page_id`, `permission_id`) VALUES
(5, 32),
(5, 61),
(6, 42),
(6, 51),
(8, 44),
(8, 49),
(9, 46),
(9, 47),
(10, 45),
(10, 48),
(24, 93),
(24, 94),
(28, 43),
(28, 50);

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `permissions`
--

CREATE TABLE `permissions` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(150) NOT NULL,
  `description` varchar(255) NOT NULL DEFAULT '',
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `permissions`
--

INSERT INTO `permissions` (`id`, `name`, `description`, `created_at`, `updated_at`) VALUES
(16, 'menu.dashboard', '', '2026-04-30 07:47:54', '2026-04-30 07:47:54'),
(25, 'menu.ustawienia', '', '2026-04-30 07:50:40', '2026-04-30 07:50:40'),
(26, 'menu.uzytkownicy', '', '2026-04-30 07:51:20', '2026-04-30 07:51:20'),
(27, 'menu.role', '', '2026-04-30 07:51:41', '2026-04-30 07:51:41'),
(28, 'menu.uprawnienia', '', '2026-04-30 07:51:52', '2026-04-30 07:51:52'),
(29, 'menu.strony', '', '2026-04-30 07:52:03', '2026-04-30 07:52:03'),
(30, 'menu.menu', '', '2026-04-30 07:52:12', '2026-04-30 07:52:12'),
(31, 'menu.phpmyadmin', '', '2026-04-30 07:52:26', '2026-04-30 07:52:26'),
(32, 'pages.dashboard.view', '', '2026-04-30 08:52:14', '2026-04-30 08:52:14'),
(42, 'pages.uzytkownicy.view', '', '2026-04-30 08:55:34', '2026-04-30 08:55:34'),
(43, 'pages.role.view', '', '2026-04-30 08:55:45', '2026-04-30 08:55:45'),
(44, 'pages.uprawnienia.view', '', '2026-04-30 08:55:57', '2026-04-30 08:55:57'),
(45, 'pages.strony.view', '', '2026-04-30 08:56:08', '2026-04-30 08:56:08'),
(46, 'pages.menu.view', '', '2026-04-30 08:56:19', '2026-04-30 08:56:19'),
(47, 'pages.menu.edit', '', '2026-04-30 08:56:39', '2026-04-30 08:56:39'),
(48, 'pages.strony.edit', '', '2026-04-30 08:56:51', '2026-04-30 08:56:51'),
(49, 'pages.uprawnienia.edit', '', '2026-04-30 08:57:05', '2026-04-30 08:57:05'),
(50, 'pages.role.edit', '', '2026-04-30 08:57:15', '2026-04-30 08:57:15'),
(51, 'pages.uzytkownicy.edit', '', '2026-04-30 08:57:27', '2026-04-30 08:57:27'),
(61, 'pages.dashboard.edit', '', '2026-04-30 08:59:44', '2026-04-30 08:59:44'),
(92, 'menu.change_password', '', '2026-05-13 12:01:15', '2026-05-13 12:01:15'),
(93, 'pages.change_password.view', '', '2026-05-13 12:01:30', '2026-05-13 12:01:30'),
(94, 'pages.change_password.edit', '', '2026-05-13 12:01:45', '2026-05-13 12:01:45');

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `rfid_tags`
--

CREATE TABLE `rfid_tags` (
  `id` int(10) UNSIGNED NOT NULL,
  `rfid_code` varchar(64) NOT NULL,
  `status` enum('ACTIVE','LOST','DAMAGED','RETIRED') NOT NULL DEFAULT 'ACTIVE',
  `notes` varchar(500) DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `roles`
--

CREATE TABLE `roles` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(100) NOT NULL,
  `description` varchar(255) NOT NULL DEFAULT '',
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `roles`
--

INSERT INTO `roles` (`id`, `name`, `description`, `created_at`, `updated_at`) VALUES
(1, 'Administrator', 'Pełny dostęp do systemu', '2026-04-17 08:44:21', '2026-04-17 08:44:21'),
(26, 'Użytkownik', '', '2026-07-06 07:52:52', '2026-07-06 07:52:52');

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `role_permissions`
--

CREATE TABLE `role_permissions` (
  `role_id` int(10) UNSIGNED NOT NULL,
  `permission_id` int(10) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `role_permissions`
--

INSERT INTO `role_permissions` (`role_id`, `permission_id`) VALUES
(1, 16),
(1, 25),
(1, 26),
(1, 27),
(1, 28),
(1, 29),
(1, 30),
(1, 31),
(1, 32),
(1, 42),
(1, 43),
(1, 44),
(1, 45),
(1, 46),
(1, 47),
(1, 48),
(1, 49),
(1, 50),
(1, 51),
(1, 61),
(1, 92),
(1, 93),
(1, 94),
(26, 16),
(26, 32),
(26, 61),
(26, 92),
(26, 93),
(26, 94);

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `users`
--

CREATE TABLE `users` (
  `id` int(10) UNSIGNED NOT NULL,
  `username` varchar(100) NOT NULL,
  `email` varchar(190) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `first_name` varchar(100) NOT NULL DEFAULT '',
  `last_name` varchar(100) NOT NULL DEFAULT '',
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `last_login_at` datetime DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `users`
--

INSERT INTO `users` (`id`, `username`, `email`, `password_hash`, `first_name`, `last_name`, `is_active`, `last_login_at`, `created_at`, `updated_at`) VALUES
(1, 'admin', 'admin@example.com', '$2y$10$KWdaLoskAuz5WpcIc2v1dOyuF.M8tem/ZcatPa1IHIcbJKpcAl6UO', 'System', 'Administrator', 1, '2026-07-07 12:51:51', '2026-04-17 08:44:21', '2026-07-07 10:51:51');

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `user_roles`
--

CREATE TABLE `user_roles` (
  `user_id` int(10) UNSIGNED NOT NULL,
  `role_id` int(10) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Zrzut danych tabeli `user_roles`
--

INSERT INTO `user_roles` (`user_id`, `role_id`) VALUES
(1, 1);

--
-- Indeksy dla zrzutów tabel
--

--
-- Indeksy dla tabeli `app_settings`
--
ALTER TABLE `app_settings`
  ADD PRIMARY KEY (`setting_key`);

--
-- Indeksy dla tabeli `buildings`
--
ALTER TABLE `buildings`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_buildings_name` (`name`),
  ADD KEY `idx_buildings_is_active` (`is_active`);

--
-- Indeksy dla tabeli `keys`
--
ALTER TABLE `keys`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_keys_name` (`name`),
  ADD KEY `idx_keys_is_active` (`is_active`),
  ADD KEY `idx_keys_building_id` (`building_id`);

--
-- Indeksy dla tabeli `key_loans`
--
ALTER TABLE `key_loans`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_key_loans_key_id` (`key_id`),
  ADD KEY `idx_key_loans_rfid_tag_id` (`rfid_tag_id`),
  ADD KEY `idx_key_loans_open` (`key_id`,`returned_at`);

--
-- Indeksy dla tabeli `key_logs`
--
ALTER TABLE `key_logs`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_key_logs_key_id` (`key_id`),
  ADD KEY `idx_key_logs_rfid_tag_id` (`rfid_tag_id`),
  ADD KEY `idx_key_logs_created_at` (`created_at`);

--
-- Indeksy dla tabeli `key_rfid_assignments`
--
ALTER TABLE `key_rfid_assignments`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_key_rfid_assignments_key_id` (`key_id`),
  ADD KEY `idx_key_rfid_assignments_rfid_tag_id` (`rfid_tag_id`),
  ADD KEY `idx_key_rfid_assignments_key_open` (`key_id`,`assigned_to`),
  ADD KEY `idx_key_rfid_assignments_rfid_open` (`rfid_tag_id`,`assigned_to`);

--
-- Indeksy dla tabeli `menu_items`
--
ALTER TABLE `menu_items`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_menu_items_parent` (`parent_id`),
  ADD KEY `idx_menu_items_page` (`page_id`),
  ADD KEY `idx_menu_items_group_visible_sort` (`menu_group`,`is_visible`,`sort_order`);

--
-- Indeksy dla tabeli `menu_item_permissions`
--
ALTER TABLE `menu_item_permissions`
  ADD PRIMARY KEY (`menu_item_id`,`permission_id`),
  ADD KEY `fk_menu_item_permissions_permission` (`permission_id`);

--
-- Indeksy dla tabeli `pages`
--
ALTER TABLE `pages`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `slug` (`slug`),
  ADD KEY `idx_pages_is_public` (`is_public`),
  ADD KEY `idx_pages_is_active` (`is_active`);

--
-- Indeksy dla tabeli `page_permissions`
--
ALTER TABLE `page_permissions`
  ADD PRIMARY KEY (`page_id`,`permission_id`),
  ADD KEY `fk_page_permissions_permission` (`permission_id`);

--
-- Indeksy dla tabeli `permissions`
--
ALTER TABLE `permissions`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `name` (`name`);

--
-- Indeksy dla tabeli `rfid_tags`
--
ALTER TABLE `rfid_tags`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_rfid_tags_code` (`rfid_code`),
  ADD KEY `idx_rfid_tags_status` (`status`);

--
-- Indeksy dla tabeli `roles`
--
ALTER TABLE `roles`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `name` (`name`);

--
-- Indeksy dla tabeli `role_permissions`
--
ALTER TABLE `role_permissions`
  ADD PRIMARY KEY (`role_id`,`permission_id`),
  ADD KEY `fk_role_permissions_permission` (`permission_id`);

--
-- Indeksy dla tabeli `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `username` (`username`),
  ADD UNIQUE KEY `email` (`email`);

--
-- Indeksy dla tabeli `user_roles`
--
ALTER TABLE `user_roles`
  ADD PRIMARY KEY (`user_id`,`role_id`),
  ADD KEY `fk_user_roles_role` (`role_id`);

--
-- AUTO_INCREMENT dla zrzuconych tabel
--


--
-- AUTO_INCREMENT dla tabeli `menu_items`
--
ALTER TABLE `menu_items`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=30;

--
-- AUTO_INCREMENT dla tabeli `pages`
--
ALTER TABLE `pages`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=35;

--
-- AUTO_INCREMENT dla tabeli `permissions`
--
ALTER TABLE `permissions`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=101;

--
-- AUTO_INCREMENT dla tabeli `roles`
--
ALTER TABLE `roles`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=27;

--
-- AUTO_INCREMENT dla tabeli `users`
--
ALTER TABLE `users`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;

--
-- Ograniczenia dla zrzutów tabel
--

--
-- Ograniczenia dla tabeli `keys`
--
ALTER TABLE `keys`
  ADD CONSTRAINT `fk_keys_building` FOREIGN KEY (`building_id`) REFERENCES `buildings` (`id`);

--
-- Ograniczenia dla tabeli `key_loans`
--
ALTER TABLE `key_loans`
  ADD CONSTRAINT `fk_key_loans_key` FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_key_loans_rfid_tag` FOREIGN KEY (`rfid_tag_id`) REFERENCES `rfid_tags` (`id`) ON DELETE SET NULL;

--
-- Ograniczenia dla tabeli `key_logs`
--
ALTER TABLE `key_logs`
  ADD CONSTRAINT `fk_key_logs_key` FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_key_logs_rfid_tag` FOREIGN KEY (`rfid_tag_id`) REFERENCES `rfid_tags` (`id`) ON DELETE SET NULL;

--
-- Ograniczenia dla tabeli `key_rfid_assignments`
--
ALTER TABLE `key_rfid_assignments`
  ADD CONSTRAINT `fk_key_rfid_assignments_key` FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_key_rfid_assignments_rfid` FOREIGN KEY (`rfid_tag_id`) REFERENCES `rfid_tags` (`id`) ON DELETE CASCADE;

--
-- Ograniczenia dla tabeli `menu_items`
--
ALTER TABLE `menu_items`
  ADD CONSTRAINT `fk_menu_items_page` FOREIGN KEY (`page_id`) REFERENCES `pages` (`id`) ON DELETE SET NULL,
  ADD CONSTRAINT `fk_menu_items_parent` FOREIGN KEY (`parent_id`) REFERENCES `menu_items` (`id`) ON DELETE CASCADE;

--
-- Ograniczenia dla tabeli `menu_item_permissions`
--
ALTER TABLE `menu_item_permissions`
  ADD CONSTRAINT `fk_menu_item_permissions_item` FOREIGN KEY (`menu_item_id`) REFERENCES `menu_items` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_menu_item_permissions_permission` FOREIGN KEY (`permission_id`) REFERENCES `permissions` (`id`) ON DELETE CASCADE;

--
-- Ograniczenia dla tabeli `page_permissions`
--
ALTER TABLE `page_permissions`
  ADD CONSTRAINT `fk_page_permissions_page` FOREIGN KEY (`page_id`) REFERENCES `pages` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_page_permissions_permission` FOREIGN KEY (`permission_id`) REFERENCES `permissions` (`id`) ON DELETE CASCADE;

--
-- Ograniczenia dla tabeli `role_permissions`
--
ALTER TABLE `role_permissions`
  ADD CONSTRAINT `fk_role_permissions_permission` FOREIGN KEY (`permission_id`) REFERENCES `permissions` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_role_permissions_role` FOREIGN KEY (`role_id`) REFERENCES `roles` (`id`) ON DELETE CASCADE;

--
-- Ograniczenia dla tabeli `user_roles`
--
ALTER TABLE `user_roles`
  ADD CONSTRAINT `fk_user_roles_role` FOREIGN KEY (`role_id`) REFERENCES `roles` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_user_roles_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
