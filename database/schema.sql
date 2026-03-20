-- phpMyAdmin SQL Dump
-- version 5.2.3
-- https://www.phpmyadmin.net/
--
-- Host: localhost
-- Generation Time: Mar 20, 2026 at 10:57 AM
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
-- Struktura tabeli dla tabeli `keys`
--

CREATE TABLE `keys` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(150) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `rfid_tag` varchar(64) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Struktura tabeli dla tabeli `key_loans`
--

CREATE TABLE `key_loans` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `key_id` int(10) UNSIGNED NOT NULL,
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
  `action_type` varchar(50) NOT NULL,
  `action_details` varchar(1000) DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Indeksy dla zrzutów tabel
--

--
-- Indeksy dla tabeli `keys`
--
ALTER TABLE `keys`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_keys_name` (`name`),
  ADD UNIQUE KEY `uq_keys_rfid_tag` (`rfid_tag`),
  ADD KEY `idx_keys_is_active` (`is_active`);

--
-- Indeksy dla tabeli `key_loans`
--
ALTER TABLE `key_loans`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_key_loans_key_id` (`key_id`),
  ADD KEY `idx_key_loans_open` (`key_id`,`returned_at`);

--
-- Indeksy dla tabeli `key_logs`
--
ALTER TABLE `key_logs`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_key_logs_key_id` (`key_id`),
  ADD KEY `idx_key_logs_created_at` (`created_at`);

--
-- AUTO_INCREMENT dla zrzuconych tabel
--

--
-- AUTO_INCREMENT dla tabeli `keys`
--
ALTER TABLE `keys`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT dla tabeli `key_loans`
--
ALTER TABLE `key_loans`
  MODIFY `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT dla tabeli `key_logs`
--
ALTER TABLE `key_logs`
  MODIFY `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- Ograniczenia dla zrzutów tabel
--

--
-- Ograniczenia dla tabeli `key_loans`
--
ALTER TABLE `key_loans`
  ADD CONSTRAINT `fk_key_loans_key` FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE;

--
-- Ograniczenia dla tabeli `key_logs`
--
ALTER TABLE `key_logs`
  ADD CONSTRAINT `fk_key_logs_key` FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
