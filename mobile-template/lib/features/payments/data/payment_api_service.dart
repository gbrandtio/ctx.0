import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../../core/constants/api_constants.dart';
import '../../../data/services/api/mixins/api_base_mixin.dart';

/// Payment endpoints (api-template/docs/features/PAYMENTS_STRIPE.md §3).
/// The API loads the referenced server-side order, so the client sends
/// only the order ID — never an amount.
class PaymentApiService with ApiBaseMixin {
  PaymentApiService(this._client);

  final http.Client _client;

  Future<String> createPaymentIntent({required String orderId}) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.paymentIntents),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'orderId': orderId}),
    );
    final json = decodeResponse(response) as Map<String, dynamic>;
    return json['clientSecret'] as String;
  }
}
