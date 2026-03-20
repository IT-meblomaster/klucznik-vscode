SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";

DROP TABLE IF EXISTS `key_logs`;
DROP TABLE IF EXISTS `key_loans`;
DROP TABLE IF EXISTS `key_rfid_assignments`;
DROP TABLE IF EXISTS `rfid_tags`;
DROP TABLE IF EXISTS `keys`;

CREATE TABLE `keys` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` varchar(150) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_keys_name` (`name`),
  KEY `idx_keys_is_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `rfid_tags` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `rfid_code` varchar(64) NOT NULL,
  `status` enum('ACTIVE','LOST','DAMAGED','RETIRED') NOT NULL DEFAULT 'ACTIVE',
  `notes` varchar(500) DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `updated_at` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_rfid_tags_code` (`rfid_code`),
  KEY `idx_rfid_tags_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `key_rfid_assignments` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `key_id` int(10) UNSIGNED NOT NULL,
  `rfid_tag_id` int(10) UNSIGNED NOT NULL,
  `assigned_from` datetime NOT NULL DEFAULT current_timestamp(),
  `assigned_to` datetime DEFAULT NULL,
  `assigned_by` varchar(255) DEFAULT NULL,
  `unassigned_reason` varchar(255) DEFAULT NULL,
  `notes` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_key_rfid_assignments_key_id` (`key_id`),
  KEY `idx_key_rfid_assignments_rfid_tag_id` (`rfid_tag_id`),
  KEY `idx_key_rfid_assignments_key_open` (`key_id`,`assigned_to`),
  KEY `idx_key_rfid_assignments_rfid_open` (`rfid_tag_id`,`assigned_to`),
  CONSTRAINT `fk_key_rfid_assignments_key`
    FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_key_rfid_assignments_rfid`
    FOREIGN KEY (`rfid_tag_id`) REFERENCES `rfid_tags` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `key_loans` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `key_id` int(10) UNSIGNED NOT NULL,
  `rfid_tag_id` int(10) UNSIGNED DEFAULT NULL,
  `issued_to_card` varchar(64) NOT NULL,
  `issued_to_name` varchar(255) NOT NULL,
  `issued_at` datetime NOT NULL DEFAULT current_timestamp(),
  `returned_by_card` varchar(64) DEFAULT NULL,
  `returned_by_name` varchar(255) DEFAULT NULL,
  `returned_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_key_loans_key_id` (`key_id`),
  KEY `idx_key_loans_rfid_tag_id` (`rfid_tag_id`),
  KEY `idx_key_loans_open` (`key_id`,`returned_at`),
  CONSTRAINT `fk_key_loans_key`
    FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_key_loans_rfid_tag`
    FOREIGN KEY (`rfid_tag_id`) REFERENCES `rfid_tags` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `key_logs` (
  `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `key_id` int(10) UNSIGNED NOT NULL,
  `rfid_tag_id` int(10) UNSIGNED DEFAULT NULL,
  `action_type` varchar(50) NOT NULL,
  `action_details` varchar(1000) DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  KEY `idx_key_logs_key_id` (`key_id`),
  KEY `idx_key_logs_rfid_tag_id` (`rfid_tag_id`),
  KEY `idx_key_logs_created_at` (`created_at`),
  CONSTRAINT `fk_key_logs_key`
    FOREIGN KEY (`key_id`) REFERENCES `keys` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_key_logs_rfid_tag`
    FOREIGN KEY (`rfid_tag_id`) REFERENCES `rfid_tags` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

COMMIT;